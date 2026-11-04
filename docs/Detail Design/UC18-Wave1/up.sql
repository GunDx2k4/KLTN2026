START TRANSACTION;
SET LOCAL lock_timeout = '3s';
LOCK TABLE bank_transaction, group_ledger IN ACCESS EXCLUSIVE MODE;
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM bank_transaction WHERE reference_code IS NULL)
       OR EXISTS (SELECT 1 FROM bank_transaction GROUP BY reference_code HAVING COUNT(*) > 1) THEN
        RAISE EXCEPTION 'UC18 preflight failed: legacy references require explicit review';
    END IF;
END $$;

DROP INDEX "IX_bank_transaction_reference_code";

ALTER TABLE group_ledger ADD bank_transaction_id uuid;

ALTER TABLE group_ledger ADD fee_id uuid;

ALTER TABLE bank_transaction ALTER COLUMN reference_code DROP NOT NULL;

ALTER TABLE bank_transaction ALTER COLUMN reconciliation_status SET DEFAULT 'MANUAL_CHECK';

ALTER TABLE bank_transaction ADD bank_account_id uuid;

ALTER TABLE bank_transaction ADD gateway character varying(100);

ALTER TABLE bank_transaction ADD provider_transaction_id bigint;

ALTER TABLE bank_transaction ADD reason_code character varying(100);

ALTER TABLE bank_transaction ADD reconciled_at timestamp with time zone;

ALTER TABLE bank_transaction ADD reconciled_by uuid;

ALTER TABLE bank_transaction ADD reconciliation_note character varying(2000);

ALTER TABLE bank_transaction ADD sub_account character varying(100);

ALTER TABLE bank_transaction ADD transfer_type character varying(3);

CREATE UNIQUE INDEX "IX_group_ledger_bank_transaction_id" ON group_ledger (bank_transaction_id) WHERE bank_transaction_id IS NOT NULL;

CREATE UNIQUE INDEX "IX_group_ledger_fee_id" ON group_ledger (fee_id) WHERE fee_id IS NOT NULL;

ALTER TABLE group_ledger ADD CONSTRAINT ck_group_ledger_uc18_source CHECK ((fee_id IS NULL AND bank_transaction_id IS NULL) OR (fee_id IS NOT NULL AND id_session IS NOT NULL AND amount > 0 AND transaction_type = 'INFLOW' AND category = 'SESSION_FEE'));

CREATE INDEX "IX_bank_transaction_bank_account_id_reference_code" ON bank_transaction (bank_account_id, reference_code);

CREATE UNIQUE INDEX "IX_bank_transaction_provider_transaction_id" ON bank_transaction (provider_transaction_id) WHERE provider_transaction_id IS NOT NULL;

CREATE INDEX "IX_bank_transaction_reconciled_by" ON bank_transaction (reconciled_by);

CREATE INDEX "IX_bank_transaction_reference_code" ON bank_transaction (reference_code);

ALTER TABLE bank_transaction ADD CONSTRAINT ck_bank_transaction_uc18 CHECK (provider_transaction_id IS NULL OR (provider_transaction_id > 0 AND bank_account_id IS NOT NULL AND gateway IS NOT NULL AND sub_account IS NOT NULL AND transfer_type IS NOT NULL AND transfer_type IN ('in', 'out') AND amount_received > 0 AND amount_received = trunc(amount_received) AND (reconciliation_status <> 'MATCHED' OR (id_fee IS NOT NULL AND reconciled_at IS NOT NULL)) AND ((reconciled_by IS NULL AND reconciliation_note IS NULL) OR (reconciled_by IS NOT NULL AND reconciliation_note IS NOT NULL AND length(btrim(reconciliation_note)) > 0))));

ALTER TABLE bank_transaction ADD CONSTRAINT "FK_bank_transaction_app_user_reconciled_by" FOREIGN KEY (reconciled_by) REFERENCES app_user (id_user) ON DELETE RESTRICT;

ALTER TABLE bank_transaction ADD CONSTRAINT "FK_bank_transaction_group_bank_account_bank_account_id" FOREIGN KEY (bank_account_id) REFERENCES group_bank_account (id_bank_account) ON DELETE RESTRICT;

ALTER TABLE group_ledger ADD CONSTRAINT "FK_group_ledger_bank_transaction_bank_transaction_id" FOREIGN KEY (bank_transaction_id) REFERENCES bank_transaction (id_transaction) ON DELETE RESTRICT;

ALTER TABLE group_ledger ADD CONSTRAINT "FK_group_ledger_session_fee_fee_id" FOREIGN KEY (fee_id) REFERENCES session_fee (id_fee) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261104074142_AddPaymentReconciliation', '10.0.10');

COMMIT;

