-- Run on Supabase/Postgres before deploying: adds a per-participation snapshot of the
-- ProctoringSettings values that were in effect when the student started the exam, so
-- changing settings mid-exam only affects students who join AFTER the change, not students
-- already taking the exam. Nullable: existing rows created before this migration stay NULL,
-- and BE code falls back to ProctoringSettingsService.GetEffectiveAsync for those.

ALTER TABLE exam_participations
    ADD COLUMN IF NOT EXISTS max_ai_violation_count_snapshot integer,
    ADD COLUMN IF NOT EXISTS cooldown_seconds_snapshot integer,
    ADD COLUMN IF NOT EXISTS allow_consecutive_same_type_snapshot boolean,
    ADD COLUMN IF NOT EXISTS ai_notify_threshold_snapshot integer,
    ADD COLUMN IF NOT EXISTS browser_notify_threshold_snapshot integer;
