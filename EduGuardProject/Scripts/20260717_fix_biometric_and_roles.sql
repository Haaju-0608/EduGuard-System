-- Run on Supabase/Postgres if biometric-requests or role checks fail.

-- 1) Legacy lowercase app_role values (if DB stores "lecturer" instead of "LECTURER")
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_enum e JOIN pg_type t ON e.enumtypid = t.oid WHERE t.typname = 'app_role' AND e.enumlabel = 'lecturer') THEN
        ALTER TYPE app_role ADD VALUE IF NOT EXISTS 'lecturer';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_enum e JOIN pg_type t ON e.enumtypid = t.oid WHERE t.typname = 'app_role' AND e.enumlabel = 'student') THEN
        ALTER TYPE app_role ADD VALUE IF NOT EXISTS 'student';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_enum e JOIN pg_type t ON e.enumtypid = t.oid WHERE t.typname = 'app_role' AND e.enumlabel = 'school_admin') THEN
        ALTER TYPE app_role ADD VALUE IF NOT EXISTS 'school_admin';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_enum e JOIN pg_type t ON e.enumtypid = t.oid WHERE t.typname = 'app_role' AND e.enumlabel = 'super_admin') THEN
        ALTER TYPE app_role ADD VALUE IF NOT EXISTS 'super_admin';
    END IF;
END $$;

-- 2) Biometric request image paths
ALTER TABLE biometric_requests
    ADD COLUMN IF NOT EXISTS front_image_path text,
    ADD COLUMN IF NOT EXISTS left_image_path text,
    ADD COLUMN IF NOT EXISTS right_image_path text;

-- 3) Link biometric_data to biometric_requests
ALTER TABLE biometric_data
    ADD COLUMN IF NOT EXISTS bio_request_id uuid;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'biometric_data_bio_request_id_fkey'
    ) THEN
        ALTER TABLE biometric_data
            ADD CONSTRAINT biometric_data_bio_request_id_fkey
            FOREIGN KEY (bio_request_id) REFERENCES biometric_requests(id) ON DELETE SET NULL;
    END IF;
END $$;
