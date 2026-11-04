START TRANSACTION;
SET LOCAL lock_timeout = '3s';
LOCK TABLE bank_transaction, group_ledger IN ACCESS EXCLUSIVE MODE;
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM bank_transaction WHERE provider_transaction_id IS NOT NULL
        OR bank_account_id IS NOT NULL OR gateway IS NOT NULL OR sub_account IS NOT NULL
        OR transfer_type IS NOT NULL OR reason_code IS NOT NULL OR reconciled_by IS NOT NULL
        OR reconciled_at IS NOT NULL OR reconciliation_note IS NOT NULL OR reference_code IS NULL)
       OR EXISTS (SELECT 1 FROM group_ledger WHERE fee_id IS NOT NULL OR bank_transaction_id IS NOT NULL)
       OR EXISTS (SELECT 1 FROM bank_transaction GROUP BY reference_code HAVING COUNT(*) > 1) THEN
        RAISE EXCEPTION 'UC18 Down blocked: reconciliation evidence or incompatible references exist';
    END IF;
END $$;

ALTER TABLE bank_transaction DROP CONSTRAINT "FK_bank_transaction_app_user_reconciled_by";

ALTER TABLE bank_transaction DROP CONSTRAINT "FK_bank_transaction_group_bank_account_bank_account_id";

ALTER TABLE group_ledger DROP CONSTRAINT "FK_group_ledger_bank_transaction_bank_transaction_id";

ALTER TABLE group_ledger DROP CONSTRAINT "FK_group_ledger_session_fee_fee_id";

DROP INDEX "IX_group_ledger_bank_transaction_id";

DROP INDEX "IX_group_ledger_fee_id";

ALTER TABLE group_ledger DROP CONSTRAINT ck_group_ledger_uc18_source;

DROP INDEX "IX_bank_transaction_bank_account_id_reference_code";

DROP INDEX "IX_bank_transaction_provider_transaction_id";

DROP INDEX "IX_bank_transaction_reconciled_by";

DROP INDEX "IX_bank_transaction_reference_code";

ALTER TABLE bank_transaction DROP CONSTRAINT ck_bank_transaction_uc18;

ALTER TABLE group_ledger DROP COLUMN bank_transaction_id;

ALTER TABLE group_ledger DROP COLUMN fee_id;

ALTER TABLE bank_transaction DROP COLUMN bank_account_id;

ALTER TABLE bank_transaction DROP COLUMN gateway;

ALTER TABLE bank_transaction DROP COLUMN provider_transaction_id;

ALTER TABLE bank_transaction DROP COLUMN reason_code;

ALTER TABLE bank_transaction DROP COLUMN reconciled_at;

ALTER TABLE bank_transaction DROP COLUMN reconciled_by;

ALTER TABLE bank_transaction DROP COLUMN reconciliation_note;

ALTER TABLE bank_transaction DROP COLUMN sub_account;

ALTER TABLE bank_transaction DROP COLUMN transfer_type;

UPDATE bank_transaction SET reference_code = '' WHERE reference_code IS NULL;
ALTER TABLE bank_transaction ALTER COLUMN reference_code SET NOT NULL;
ALTER TABLE bank_transaction ALTER COLUMN reference_code SET DEFAULT '';

ALTER TABLE bank_transaction ALTER COLUMN reconciliation_status SET DEFAULT 'MATCHED';

CREATE UNIQUE INDEX "IX_bank_transaction_reference_code" ON bank_transaction (reference_code);

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261104074142_AddPaymentReconciliation';

COMMIT;

