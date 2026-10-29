START TRANSACTION;
ALTER TABLE session_fee ADD adjusted_at timestamp with time zone;

ALTER TABLE session_fee ADD adjusted_by uuid;

ALTER TABLE session_fee ADD adjustment_delta numeric(18,2);

ALTER TABLE session_fee ADD adjustment_reason character varying(2000);

ALTER TABLE session_fee ADD base_amount numeric(18,2);

ALTER TABLE session_fee ADD calculated_amount numeric(18,2);

ALTER TABLE session_fee ADD court_share numeric(18,2);

ALTER TABLE session_fee ADD extra_share numeric(18,2);

ALTER TABLE session_fee ADD gender smallint;

ALTER TABLE session_fee ADD is_published boolean NOT NULL DEFAULT FALSE;

ALTER TABLE session_fee ADD member_type character varying(50);

ALTER TABLE session_fee ADD monthly_expiry_date timestamp with time zone;

ALTER TABLE session_fee ADD monthly_fund_covered numeric(18,2);

ALTER TABLE session_fee ADD published_at timestamp with time zone;

ALTER TABLE session_fee ADD published_by uuid;

ALTER TABLE session_fee ADD raw_amount numeric(18,2);

ALTER TABLE session_fee ADD rounding_delta numeric(18,2);

ALTER TABLE session_fee ADD shuttle_share numeric(18,2);

ALTER TABLE badminton_session ADD adjustment_delta numeric(18,2);

ALTER TABLE badminton_session ADD balance_difference numeric(18,2);

ALTER TABLE badminton_session ADD monthly_fund_covered numeric(18,2);

ALTER TABLE badminton_session ADD policy_delta numeric(18,2);

ALTER TABLE badminton_session ADD published_at timestamp with time zone;

ALTER TABLE badminton_session ADD published_by uuid;

ALTER TABLE badminton_session ADD published_daily_member_fee numeric(18,2);

ALTER TABLE badminton_session ADD published_fee_mode character varying(50);

ALTER TABLE badminton_session ADD published_fee_rule_applied_at timestamp with time zone;

ALTER TABLE badminton_session ADD published_fee_rule_id uuid;

ALTER TABLE badminton_session ADD published_female_discount_daily numeric(18,2);

ALTER TABLE badminton_session ADD published_female_discount_guest numeric(18,2);

ALTER TABLE badminton_session ADD published_guest_fee numeric(18,2);

ALTER TABLE badminton_session ADD published_preview_id uuid;

ALTER TABLE badminton_session ADD published_request_hash character varying(64);

ALTER TABLE badminton_session ADD published_round_rule integer;

ALTER TABLE badminton_session ADD rounding_delta numeric(18,2);

ALTER TABLE badminton_session ADD shuttlecock_price numeric(18,2);

ALTER TABLE badminton_session ADD total_cost numeric(18,2);

ALTER TABLE badminton_session ADD total_discount numeric(18,2);

ALTER TABLE badminton_session ADD total_receivable numeric(18,2);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261029135900_AddSettlementPublicationSnapshot', '10.0.10');

COMMIT;

