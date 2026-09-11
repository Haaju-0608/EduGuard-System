-- Run on Supabase/Postgres before deploying: adds configurable AI proctoring behavior
-- (per-violation-type detection timing, AI violation cap, cooldown between AI violations,
-- consecutive-same-type toggle, and lecturer-notify thresholds for both AI and browser
-- violations, replacing the old hard-coded auto-disqualify-at-3 for browser violations).

CREATE TABLE IF NOT EXISTS proctoring_settings (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    institution_id uuid REFERENCES institutions(id) ON DELETE CASCADE,
    max_ai_violation_count integer NOT NULL DEFAULT 10,
    cooldown_seconds integer NOT NULL DEFAULT 5,
    allow_consecutive_same_type boolean NOT NULL DEFAULT false,
    ai_notify_threshold integer NOT NULL DEFAULT 3,
    browser_notify_threshold integer NOT NULL DEFAULT 3,
    is_active boolean NOT NULL DEFAULT true,
    created_by uuid REFERENCES users(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES users(id) ON DELETE SET NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_proctoring_settings_institution ON proctoring_settings(institution_id);

CREATE TABLE IF NOT EXISTS proctoring_violation_type_settings (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    proctoring_settings_id uuid NOT NULL REFERENCES proctoring_settings(id) ON DELETE CASCADE,
    violation_type violation_type NOT NULL,
    detection_threshold_seconds integer NOT NULL DEFAULT 3
);

CREATE UNIQUE INDEX IF NOT EXISTS proctoring_violation_type_settings_unique
    ON proctoring_violation_type_settings(proctoring_settings_id, violation_type);

-- Seed the system-wide default config (institution_id = NULL) so the app has a fallback
-- as soon as this script runs, before any School Admin configures their own override.
INSERT INTO proctoring_settings (
    institution_id, max_ai_violation_count, cooldown_seconds,
    allow_consecutive_same_type, ai_notify_threshold, browser_notify_threshold, is_active
)
SELECT NULL, 10, 5, false, 3, 3, true
WHERE NOT EXISTS (SELECT 1 FROM proctoring_settings WHERE institution_id IS NULL);

INSERT INTO proctoring_violation_type_settings (proctoring_settings_id, violation_type, detection_threshold_seconds)
SELECT s.id, v.violation_type::violation_type, v.threshold
FROM proctoring_settings s
CROSS JOIN (VALUES
    ('GAZE_DIVERSION', 3),
    ('MULTIPLE_FACES', 2),
    ('ABSENCE', 5),
    ('HEAD_TURN', 3),
    ('FACE_OBSTRUCTED', 3),
    ('IMPERSONATION', 1)
) AS v(violation_type, threshold)
WHERE s.institution_id IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM proctoring_violation_type_settings pvts
      WHERE pvts.proctoring_settings_id = s.id AND pvts.violation_type = v.violation_type::violation_type
  );
