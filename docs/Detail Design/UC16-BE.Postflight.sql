BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SET LOCAL statement_timeout = '30s';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory"
        WHERE "MigrationId" = '20261029135900_AddSettlementPublicationSnapshot') THEN
        RAISE EXCEPTION 'UC16 migration history entry is missing';
    END IF;
    IF EXISTS (SELECT 1 FROM session_fee f WHERE f.is_published
        AND (f.court_share IS NULL OR f.shuttle_share IS NULL OR f.extra_share IS NULL
            OR f.published_at IS NULL OR f.published_by IS NULL
            OR f.payment_code !~ '^CLB [0-9A-F]{12} [0-9A-F]{12}$'
            OR (f.amount = 0 AND f.payment_status <> 'EXEMPT')
            OR NOT EXISTS (SELECT 1 FROM badminton_session s
                WHERE s.id_session = f.id_session AND s.published_preview_id IS NOT NULL))) THEN
        RAISE EXCEPTION 'UC16 published fee invariants are inconsistent';
    END IF;
    IF EXISTS (SELECT 1 FROM badminton_session s WHERE s.published_preview_id IS NOT NULL
        AND (s.total_receivable IS DISTINCT FROM (SELECT sum(f.amount) FROM session_fee f
                 WHERE f.id_session = s.id_session AND f.is_published)
            OR s.court_rental_cost IS DISTINCT FROM (SELECT sum(f.court_share) FROM session_fee f
                 WHERE f.id_session = s.id_session AND f.is_published)
            OR s.shuttlecock_used * s.shuttlecock_price IS DISTINCT FROM
                (SELECT sum(f.shuttle_share) FROM session_fee f
                 WHERE f.id_session = s.id_session AND f.is_published)
            OR s.extra_expense IS DISTINCT FROM (SELECT sum(f.extra_share) FROM session_fee f
                 WHERE f.id_session = s.id_session AND f.is_published))) THEN
        RAISE EXCEPTION 'UC16 publication totals do not match fee components';
    END IF;
END $$;

SELECT count(*) AS legacy_fees, count(*) FILTER (WHERE payment_status = 'PAID') AS legacy_paid_fees,
       sum(amount) AS legacy_total_amount
FROM session_fee WHERE NOT is_published;

SELECT count(*) AS published_sessions FROM badminton_session WHERE published_preview_id IS NOT NULL;
SELECT count(*) AS bank_references FROM bank_transaction WHERE id_fee IS NOT NULL;

SELECT count(*) FILTER (WHERE delivered_at IS NULL AND attempts < 10) AS pending,
       count(*) FILTER (WHERE delivered_at IS NULL AND attempts >= 10) AS dead_letter,
       count(*) FILTER (WHERE delivered_at IS NOT NULL) AS delivered
FROM rotation_outbox WHERE kind = 'SettlementPaymentPush' AND NOT "IsDeleted";

COMMIT;
