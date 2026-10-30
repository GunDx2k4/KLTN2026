BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '30s';

SELECT current_database() AS database_name, current_user AS operator,
       inet_server_addr() AS server_address, inet_server_port() AS server_port,
       current_setting('server_version') AS postgres_version;

DO $$
BEGIN
    IF current_setting('server_version_num')::integer < 160000 THEN
        RAISE EXCEPTION 'UC16 requires PostgreSQL 16 or later';
    END IF;
    IF (SELECT max("MigrationId") FROM "__EFMigrationsHistory") IS DISTINCT FROM
        '20261026090341_AddFemaleDiscountGuestToFeeRule' THEN
        RAISE EXCEPTION 'UC16 predecessor migration does not match the approved release';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema()
        AND ((table_name = 'badminton_session' AND column_name = 'published_preview_id')
          OR (table_name = 'session_fee' AND column_name = 'is_published'))) THEN
        RAISE EXCEPTION 'UC16 snapshot columns already exist; investigate partial or prior deployment';
    END IF;
END $$;

SELECT count(*) AS legacy_fees, count(*) FILTER (WHERE payment_status = 'PAID') AS paid_fees,
       sum(amount) AS total_amount, sum(amount) FILTER (WHERE payment_status = 'PAID') AS paid_amount
FROM session_fee;

SELECT count(*) AS bank_references FROM bank_transaction WHERE id_fee IS NOT NULL;

SELECT id_group, id_user FROM group_member
WHERE member_type = 'MONTHLY' AND status = 'ACTIVE' AND NOT "IsDeleted"
  AND monthly_expiry_date IS NULL ORDER BY id_group, id_user;

SELECT indexname, indexdef FROM pg_indexes
WHERE schemaname = current_schema() AND tablename = 'session_fee' ORDER BY indexname;

COMMIT;
