START TRANSACTION;
ALTER TABLE session_fee DROP COLUMN adjusted_at;

ALTER TABLE session_fee DROP COLUMN adjusted_by;

ALTER TABLE session_fee DROP COLUMN adjustment_delta;

ALTER TABLE session_fee DROP COLUMN adjustment_reason;

ALTER TABLE session_fee DROP COLUMN base_amount;

ALTER TABLE session_fee DROP COLUMN calculated_amount;

ALTER TABLE session_fee DROP COLUMN court_share;

ALTER TABLE session_fee DROP COLUMN extra_share;

ALTER TABLE session_fee DROP COLUMN gender;

ALTER TABLE session_fee DROP COLUMN is_published;

ALTER TABLE session_fee DROP COLUMN member_type;

ALTER TABLE session_fee DROP COLUMN monthly_expiry_date;

ALTER TABLE session_fee DROP COLUMN monthly_fund_covered;

ALTER TABLE session_fee DROP COLUMN published_at;

ALTER TABLE session_fee DROP COLUMN published_by;

ALTER TABLE session_fee DROP COLUMN raw_amount;

ALTER TABLE session_fee DROP COLUMN rounding_delta;

ALTER TABLE session_fee DROP COLUMN shuttle_share;

ALTER TABLE badminton_session DROP COLUMN adjustment_delta;

ALTER TABLE badminton_session DROP COLUMN balance_difference;

ALTER TABLE badminton_session DROP COLUMN monthly_fund_covered;

ALTER TABLE badminton_session DROP COLUMN policy_delta;

ALTER TABLE badminton_session DROP COLUMN published_at;

ALTER TABLE badminton_session DROP COLUMN published_by;

ALTER TABLE badminton_session DROP COLUMN published_daily_member_fee;

ALTER TABLE badminton_session DROP COLUMN published_fee_mode;

ALTER TABLE badminton_session DROP COLUMN published_fee_rule_applied_at;

ALTER TABLE badminton_session DROP COLUMN published_fee_rule_id;

ALTER TABLE badminton_session DROP COLUMN published_female_discount_daily;

ALTER TABLE badminton_session DROP COLUMN published_female_discount_guest;

ALTER TABLE badminton_session DROP COLUMN published_guest_fee;

ALTER TABLE badminton_session DROP COLUMN published_preview_id;

ALTER TABLE badminton_session DROP COLUMN published_request_hash;

ALTER TABLE badminton_session DROP COLUMN published_round_rule;

ALTER TABLE badminton_session DROP COLUMN rounding_delta;

ALTER TABLE badminton_session DROP COLUMN shuttlecock_price;

ALTER TABLE badminton_session DROP COLUMN total_cost;

ALTER TABLE badminton_session DROP COLUMN total_discount;

ALTER TABLE badminton_session DROP COLUMN total_receivable;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261029135900_AddSettlementPublicationSnapshot';

COMMIT;

