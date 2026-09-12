-- Run on Supabase/Postgres before deploying: allows fractional detection thresholds
-- (e.g. 1.2s, 1.5s, 1.8s) for AI Proctoring violation types. Previously `integer`, which
-- rejected/rounded decimal values entered on the SchoolAdmin settings page.

ALTER TABLE proctoring_violation_type_settings
    ALTER COLUMN detection_threshold_seconds TYPE double precision
    USING detection_threshold_seconds::double precision;
