-- =====================================================================
-- EduGuard — full database schema (PostgreSQL / Supabase)
-- Generated from the current EF Core model (AppDbContext) on 2026-09-25.
-- Includes everything in Scripts/*.sql (proctor_id, browser violation types,
-- proctoring settings, participation snapshot, double detection threshold,
-- EXAM_PARTICIPATION reference type).
-- Safe to re-run: types/tables/indexes are guarded with IF NOT EXISTS.
-- Also includes (see PART 2-4 at the bottom): storage buckets, FK users.id -> auth.users.id,
-- and Row Level Security. auth.* / storage.* schemas themselves are managed by Supabase.
-- =====================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS vector;

-- ---------- ENUM TYPES ----------
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'app_role') THEN
        CREATE TYPE public.app_role AS ENUM ('STUDENT', 'LECTURER', 'SCHOOL_ADMIN', 'SUPER_ADMIN', 'student', 'lecturer', 'school_admin', 'super_admin');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'attendance_method') THEN
        CREATE TYPE public.attendance_method AS ENUM ('AI', 'MANUAL');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'attendance_status') THEN
        CREATE TYPE public.attendance_status AS ENUM ('PRESENT', 'ABSENT', 'LATE', 'EXCUSED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'billing_model_enum') THEN
        CREATE TYPE public.billing_model_enum AS ENUM ('MONTHLY', 'YEARLY');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'biometric_req_status') THEN
        CREATE TYPE public.biometric_req_status AS ENUM ('PENDING', 'APPROVED', 'REJECTED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'enrollment_status') THEN
        CREATE TYPE public.enrollment_status AS ENUM ('ACTIVE', 'DROPPED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'exam_slot_status') THEN
        CREATE TYPE public.exam_slot_status AS ENUM ('SCHEDULED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'institution_status') THEN
        CREATE TYPE public.institution_status AS ENUM ('ACTIVE', 'INACTIVE', 'SUSPENDED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'notification_channel') THEN
        CREATE TYPE public.notification_channel AS ENUM ('PUSH', 'EMAIL', 'DASHBOARD');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'notification_type') THEN
        CREATE TYPE public.notification_type AS ENUM ('LOW_BALANCE_ALERT', 'ATTENDANCE_SESSION_STARTED', 'EXAM_REMINDER', 'VIOLATION_DETECTED', 'BIOMETRIC_REQUEST_STATUS', 'SERVICE_SUSPENDED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'participation_status') THEN
        CREATE TYPE public.participation_status AS ENUM ('JOINED', 'SUBMITTED', 'DISQUALIFIED', 'ABSENT', 'LEFT');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'pricing_service_type') THEN
        CREATE TYPE public.pricing_service_type AS ENUM ('attendance_unit', 'proctoring_per_hour', 'subscription_monthly', 'subscription_yearly');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'reference_type_enum') THEN
        CREATE TYPE public.reference_type_enum AS ENUM ('INSTITUTION', 'ATTENDANCE_SESSION', 'EXAM_SLOT', 'EXAM_PARTICIPATION', 'TRANSACTION');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'session_status') THEN
        CREATE TYPE public.session_status AS ENUM ('IN_PROGRESS', 'COMPLETED', 'CANCELLED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'student_exam_record_status') THEN
        CREATE TYPE public.student_exam_record_status AS ENUM ('MARKED', 'COMPLETED', 'DELETED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'transaction_status') THEN
        CREATE TYPE public.transaction_status AS ENUM ('PENDING', 'SUCCESS', 'FAILED', 'pending', 'success', 'failed');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'transaction_type') THEN
        CREATE TYPE public.transaction_type AS ENUM ('TOP_UP', 'ATTENDANCE_FEE', 'PROCTORING_FEE', 'top_up', 'attendance_fee', 'proctoring_fee', 'SUBSCRIPTION_FEE');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'user_status') THEN
        CREATE TYPE public.user_status AS ENUM ('ACTIVE', 'BLOCKED');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'violation_severity') THEN
        CREATE TYPE public.violation_severity AS ENUM ('WARNING', 'SEVERE');
    END IF;
END $$;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'violation_type') THEN
        CREATE TYPE public.violation_type AS ENUM ('IMPERSONATION', 'GAZE_DIVERSION', 'MULTIPLE_FACES', 'ABSENCE', 'HEAD_TURN', 'FACE_OBSTRUCTED', 'TabSwitch', 'WindowBlur', 'ExitFullscreen');
    END IF;
END $$;

-- ---------- TABLES / INDEXES ----------

CREATE TABLE IF NOT EXISTS contact_requests (
    id uuid NOT NULL,
    school_name text NOT NULL,
    contact_person_name text NOT NULL,
    email text NOT NULL,
    phone_number text NOT NULL,
    message text,
    created_at timestamp with time zone NOT NULL,
    status text NOT NULL,
    CONSTRAINT "PK_contact_requests" PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS institutions (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    name character varying(255) NOT NULL,
    sub_domain character varying(50) NOT NULL,
    contact_email character varying(100) NOT NULL,
    subscription_expires_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    deleted_at timestamp with time zone,
    billing_model billing_model_enum NOT NULL,
    status institution_status NOT NULL,
    CONSTRAINT institutions_pkey PRIMARY KEY (id)
);


CREATE TABLE IF NOT EXISTS reading_passages (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    institution_id uuid NOT NULL,
    exam_question_name character varying(255) NOT NULL,
    passage_text text NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT reading_passages_pkey PRIMARY KEY (id),
    CONSTRAINT reading_passages_institution_id_fkey FOREIGN KEY (institution_id) REFERENCES institutions (id) ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS users (
    id uuid NOT NULL,
    institution_id uuid,
    student_code character varying(20),
    email character varying(100) NOT NULL,
    full_name character varying(100) NOT NULL,
    phone character varying(20),
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    deleted_at timestamp with time zone,
    status user_status NOT NULL,
    role app_role NOT NULL,
    CONSTRAINT users_pkey PRIMARY KEY (id),
    CONSTRAINT users_institution_id_fkey FOREIGN KEY (institution_id) REFERENCES institutions (id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS wallets (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    institution_id uuid NOT NULL,
    balance numeric(18,2) NOT NULL,
    currency character varying(10) NOT NULL DEFAULT ('VND'::character varying),
    low_balance_threshold numeric(18,2) NOT NULL DEFAULT (50000),
    low_balance_alert_sent_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT wallets_pkey PRIMARY KEY (id),
    CONSTRAINT wallets_institution_id_fkey FOREIGN KEY (institution_id) REFERENCES institutions (id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS exam_questions (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    institution_id uuid NOT NULL,
    exam_question_name character varying(255) NOT NULL,
    passage_id uuid,
    question_type character varying(30) NOT NULL,
    question_content text NOT NULL,
    audio_url character varying(500),
    image_url character varying(500),
    points numeric(6,2) NOT NULL DEFAULT (1),
    display_order integer NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT exam_questions_pkey PRIMARY KEY (id),
    CONSTRAINT exam_questions_institution_id_fkey FOREIGN KEY (institution_id) REFERENCES institutions (id) ON DELETE RESTRICT,
    CONSTRAINT exam_questions_passage_id_fkey FOREIGN KEY (passage_id) REFERENCES reading_passages (id) ON DELETE SET NULL
);


CREATE TABLE IF NOT EXISTS biometric_requests (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    student_id uuid NOT NULL,
    approved_by uuid,
    reason text NOT NULL,
    status biometric_req_status NOT NULL,
    front_image_path text,
    left_image_path text,
    right_image_path text,
    reviewed_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    deleted_at timestamp with time zone,
    CONSTRAINT biometric_requests_pkey PRIMARY KEY (id),
    CONSTRAINT biometric_requests_approved_by_fkey FOREIGN KEY (approved_by) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT biometric_requests_student_id_fkey FOREIGN KEY (student_id) REFERENCES users (id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS classes (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    institution_id uuid NOT NULL,
    lecturer_id uuid NOT NULL,
    course_name character varying(255) NOT NULL,
    course_code character varying(50),
    semester character varying(50) NOT NULL,
    academic_year character varying(20) NOT NULL,
    start_date date,
    end_date date,
    created_by uuid,
    updated_by uuid,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    deleted_at timestamp with time zone,
    CONSTRAINT classes_pkey PRIMARY KEY (id),
    CONSTRAINT classes_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT classes_institution_id_fkey FOREIGN KEY (institution_id) REFERENCES institutions (id) ON DELETE CASCADE,
    CONSTRAINT classes_lecturer_id_fkey FOREIGN KEY (lecturer_id) REFERENCES users (id) ON DELETE RESTRICT,
    CONSTRAINT classes_updated_by_fkey FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE SET NULL
);


CREATE TABLE IF NOT EXISTS notifications (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    user_id uuid NOT NULL,
    title character varying(255) NOT NULL,
    body text NOT NULL,
    is_read boolean NOT NULL DEFAULT FALSE,
    reference_id uuid,
    type notification_type NOT NULL,
    reference_type reference_type_enum,
    sent_via notification_channel NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT notifications_pkey PRIMARY KEY (id),
    CONSTRAINT notifications_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS pricing_configs (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    service_type pricing_service_type NOT NULL,
    unit_price numeric(18,2) NOT NULL,
    effective_date timestamp with time zone NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_by uuid,
    updated_by uuid,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT pricing_configs_pkey PRIMARY KEY (id),
    CONSTRAINT pricing_configs_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT pricing_configs_updated_by_fkey FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE SET NULL
);
COMMENT ON COLUMN pricing_configs.unit_price IS 'Pricing in VND';


CREATE TABLE IF NOT EXISTS proctoring_settings (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    institution_id uuid,
    max_ai_violation_count integer NOT NULL,
    cooldown_seconds integer NOT NULL,
    allow_consecutive_same_type boolean NOT NULL,
    ai_notify_threshold integer NOT NULL,
    browser_notify_threshold integer NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_by uuid,
    updated_by uuid,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT proctoring_settings_pkey PRIMARY KEY (id),
    CONSTRAINT proctoring_settings_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT proctoring_settings_institution_id_fkey FOREIGN KEY (institution_id) REFERENCES institutions (id) ON DELETE CASCADE,
    CONSTRAINT proctoring_settings_updated_by_fkey FOREIGN KEY (updated_by) REFERENCES users (id) ON DELETE SET NULL
);


CREATE TABLE IF NOT EXISTS question_options (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    question_id uuid NOT NULL,
    option_label character varying(10) NOT NULL,
    option_content text NOT NULL,
    is_correct boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT question_options_pkey PRIMARY KEY (id),
    CONSTRAINT question_options_question_id_fkey FOREIGN KEY (question_id) REFERENCES exam_questions (id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS biometric_data (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    user_id uuid NOT NULL,
    bio_request_id uuid,
    face_vector vector(128),
    model_version character varying(50) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    face_image_url character varying(2000),
    CONSTRAINT biometric_data_pkey PRIMARY KEY (id),
    CONSTRAINT biometric_data_bio_request_id_fkey FOREIGN KEY (bio_request_id) REFERENCES biometric_requests (id) ON DELETE SET NULL,
    CONSTRAINT biometric_data_user_id_fkey FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);
COMMENT ON COLUMN biometric_data.model_version IS 'AI model version used to generate embedding';
COMMENT ON COLUMN biometric_data.face_image_url IS 'Bucket: biometric-faces';


CREATE TABLE IF NOT EXISTS class_enrollments (
    class_id uuid NOT NULL,
    student_id uuid NOT NULL,
    status enrollment_status NOT NULL,
    enrolled_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT class_enrollments_pkey PRIMARY KEY (class_id, student_id),
    CONSTRAINT class_enrollments_class_id_fkey FOREIGN KEY (class_id) REFERENCES classes (id) ON DELETE CASCADE,
    CONSTRAINT class_enrollments_student_id_fkey FOREIGN KEY (student_id) REFERENCES users (id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS exam_slots (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    class_id uuid NOT NULL,
    created_by uuid NOT NULL,
    exam_name character varying(255) NOT NULL,
    exam_question_name character varying(255) NOT NULL,
    start_time timestamp with time zone NOT NULL,
    end_time timestamp with time zone NOT NULL,
    expected_duration_minutes integer NOT NULL,
    status exam_slot_status NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    proctor_id uuid,
    CONSTRAINT exam_slots_pkey PRIMARY KEY (id),
    CONSTRAINT exam_slots_class_id_fkey FOREIGN KEY (class_id) REFERENCES classes (id) ON DELETE CASCADE,
    CONSTRAINT exam_slots_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE RESTRICT,
    CONSTRAINT exam_slots_proctor_id_fkey FOREIGN KEY (proctor_id) REFERENCES users (id) ON DELETE SET NULL
);


CREATE TABLE IF NOT EXISTS transactions (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    wallet_id uuid NOT NULL,
    pricing_config_id uuid,
    vnpay_ref character varying(100),
    amount numeric(18,2) NOT NULL,
    type transaction_type NOT NULL,
    status transaction_status NOT NULL,
    description text,
    processed_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT transactions_pkey PRIMARY KEY (id),
    CONSTRAINT transactions_pricing_config_id_fkey FOREIGN KEY (pricing_config_id) REFERENCES pricing_configs (id) ON DELETE SET NULL,
    CONSTRAINT transactions_wallet_id_fkey FOREIGN KEY (wallet_id) REFERENCES wallets (id) ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS proctoring_violation_type_settings (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    proctoring_settings_id uuid NOT NULL,
    violation_type violation_type NOT NULL,
    detection_threshold_seconds double precision NOT NULL,
    CONSTRAINT proctoring_violation_type_settings_pkey PRIMARY KEY (id),
    CONSTRAINT proctoring_violation_type_settings_settings_id_fkey FOREIGN KEY (proctoring_settings_id) REFERENCES proctoring_settings (id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS student_exam_records (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    exam_slot_id uuid NOT NULL,
    student_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    ended_at timestamp with time zone,
    exam_record jsonb,
    final_score numeric(5,2),
    submitted_at timestamp with time zone,
    duration_seconds integer,
    status student_exam_record_status NOT NULL DEFAULT 'MARKED'::student_exam_record_status,
    CONSTRAINT student_exam_records_pkey PRIMARY KEY (id),
    CONSTRAINT student_exam_records_exam_slot_id_fkey FOREIGN KEY (exam_slot_id) REFERENCES exam_slots (id) ON DELETE CASCADE,
    CONSTRAINT student_exam_records_student_id_fkey FOREIGN KEY (student_id) REFERENCES users (id) ON DELETE RESTRICT
);
COMMENT ON COLUMN student_exam_records.student_id IS 'Must reference a users.id whose role is STUDENT.';


CREATE TABLE IF NOT EXISTS attendance_sessions (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    class_id uuid NOT NULL,
    exam_slot_id uuid,
    created_by uuid NOT NULL,
    billing_trans_id uuid,
    video_path character varying(2000),
    total_recognized integer NOT NULL DEFAULT 0,
    start_time timestamp with time zone NOT NULL,
    end_time timestamp with time zone,
    status session_status NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT (now()),
    updated_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT attendance_sessions_pkey PRIMARY KEY (id),
    CONSTRAINT attendance_sessions_billing_trans_id_fkey FOREIGN KEY (billing_trans_id) REFERENCES transactions (id) ON DELETE SET NULL,
    CONSTRAINT attendance_sessions_class_id_fkey FOREIGN KEY (class_id) REFERENCES classes (id) ON DELETE CASCADE,
    CONSTRAINT attendance_sessions_created_by_fkey FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE RESTRICT,
    CONSTRAINT attendance_sessions_exam_slot_id_fkey FOREIGN KEY (exam_slot_id) REFERENCES exam_slots (id) ON DELETE SET NULL
);
COMMENT ON COLUMN attendance_sessions.video_path IS 'Bucket: attendance-videos';


CREATE TABLE IF NOT EXISTS exam_participations (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    exam_slot_id uuid NOT NULL,
    student_id uuid NOT NULL,
    identity_verified_at timestamp with time zone,
    identity_verified_by uuid,
    billing_trans_id uuid,
    actual_start timestamp with time zone,
    actual_end timestamp with time zone,
    status participation_status NOT NULL,
    disqualified_reason text,
    recording_video_path text,
    identity_snapshot_path text,
    max_ai_violation_count_snapshot integer,
    cooldown_seconds_snapshot integer,
    allow_consecutive_same_type_snapshot boolean,
    ai_notify_threshold_snapshot integer,
    browser_notify_threshold_snapshot integer,
    CONSTRAINT exam_participations_pkey PRIMARY KEY (id),
    CONSTRAINT exam_participations_billing_trans_id_fkey FOREIGN KEY (billing_trans_id) REFERENCES transactions (id) ON DELETE SET NULL,
    CONSTRAINT exam_participations_exam_slot_id_fkey FOREIGN KEY (exam_slot_id) REFERENCES exam_slots (id) ON DELETE CASCADE,
    CONSTRAINT exam_participations_identity_verified_by_fkey FOREIGN KEY (identity_verified_by) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT exam_participations_student_id_fkey FOREIGN KEY (student_id) REFERENCES users (id) ON DELETE CASCADE
);
COMMENT ON COLUMN exam_participations.recording_video_path IS 'Bucket: exam-recordings';
COMMENT ON COLUMN exam_participations.identity_snapshot_path IS 'Bucket: exam-identity';


CREATE TABLE IF NOT EXISTS attendance_records (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    session_id uuid NOT NULL,
    student_id uuid NOT NULL,
    confidence_score double precision,
    snapshot_path character varying(2000),
    checkin_at timestamp with time zone,
    adjusted_by uuid,
    adjusted_at timestamp with time zone,
    method attendance_method NOT NULL,
    status attendance_status NOT NULL,
    CONSTRAINT attendance_records_pkey PRIMARY KEY (id),
    CONSTRAINT attendance_records_adjusted_by_fkey FOREIGN KEY (adjusted_by) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT attendance_records_session_id_fkey FOREIGN KEY (session_id) REFERENCES attendance_sessions (id) ON DELETE CASCADE,
    CONSTRAINT attendance_records_student_id_fkey FOREIGN KEY (student_id) REFERENCES users (id) ON DELETE CASCADE
);
COMMENT ON COLUMN attendance_records.snapshot_path IS 'Bucket: attendance-snapshots';


CREATE TABLE IF NOT EXISTS violation_logs (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    participation_id uuid NOT NULL,
    evidence_path text,
    severity violation_severity NOT NULL,
    violation_type violation_type NOT NULL,
    ai_confidence double precision,
    is_reviewed boolean NOT NULL DEFAULT FALSE,
    reviewed_by uuid,
    recorded_at timestamp with time zone NOT NULL DEFAULT (now()),
    CONSTRAINT violation_logs_pkey PRIMARY KEY (id),
    CONSTRAINT violation_logs_participation_id_fkey FOREIGN KEY (participation_id) REFERENCES exam_participations (id) ON DELETE CASCADE,
    CONSTRAINT violation_logs_reviewed_by_fkey FOREIGN KEY (reviewed_by) REFERENCES users (id) ON DELETE SET NULL
);
COMMENT ON COLUMN violation_logs.evidence_path IS 'Bucket: exam-evidence';


CREATE UNIQUE INDEX IF NOT EXISTS attendance_records_session_id_student_id_key ON attendance_records (session_id, student_id);


CREATE INDEX IF NOT EXISTS idx_attendance_record_student ON attendance_records (student_id);


CREATE INDEX IF NOT EXISTS "IX_attendance_records_adjusted_by" ON attendance_records (adjusted_by);


CREATE UNIQUE INDEX IF NOT EXISTS attendance_sessions_billing_trans_id_key ON attendance_sessions (billing_trans_id);


CREATE INDEX IF NOT EXISTS idx_attendance_session_class ON attendance_sessions (class_id);


CREATE INDEX IF NOT EXISTS "IX_attendance_sessions_created_by" ON attendance_sessions (created_by);


CREATE INDEX IF NOT EXISTS "IX_attendance_sessions_exam_slot_id" ON attendance_sessions (exam_slot_id);


CREATE INDEX IF NOT EXISTS idx_biometric_face_image ON biometric_data (face_image_url);


CREATE INDEX IF NOT EXISTS "IX_biometric_data_bio_request_id" ON biometric_data (bio_request_id);


CREATE INDEX IF NOT EXISTS ux_biometric_active ON biometric_data (user_id) WHERE (is_active = true);


CREATE INDEX IF NOT EXISTS "IX_biometric_requests_approved_by" ON biometric_requests (approved_by);


CREATE INDEX IF NOT EXISTS "IX_biometric_requests_student_id" ON biometric_requests (student_id);


CREATE INDEX IF NOT EXISTS "IX_class_enrollments_student_id" ON class_enrollments (student_id);


CREATE INDEX IF NOT EXISTS idx_classes_institution ON classes (institution_id);


CREATE INDEX IF NOT EXISTS idx_classes_lecturer ON classes (lecturer_id);


CREATE INDEX IF NOT EXISTS "IX_classes_created_by" ON classes (created_by);


CREATE INDEX IF NOT EXISTS "IX_classes_updated_by" ON classes (updated_by);


CREATE UNIQUE INDEX IF NOT EXISTS exam_participations_billing_trans_id_key ON exam_participations (billing_trans_id);


CREATE UNIQUE INDEX IF NOT EXISTS exam_participations_exam_slot_id_student_id_key ON exam_participations (exam_slot_id, student_id);


CREATE INDEX IF NOT EXISTS idx_exam_participation_student ON exam_participations (student_id);


CREATE INDEX IF NOT EXISTS idx_exam_recording ON exam_participations (recording_video_path);


CREATE INDEX IF NOT EXISTS "IX_exam_participations_identity_verified_by" ON exam_participations (identity_verified_by);


CREATE INDEX IF NOT EXISTS idx_exam_questions_institution_name_order ON exam_questions (institution_id, exam_question_name, display_order);


CREATE INDEX IF NOT EXISTS idx_exam_questions_passage ON exam_questions (passage_id);


CREATE INDEX IF NOT EXISTS idx_exam_slot_class ON exam_slots (class_id);


CREATE INDEX IF NOT EXISTS idx_exam_slots_exam_question_name ON exam_slots (exam_question_name);


CREATE INDEX IF NOT EXISTS "IX_exam_slots_created_by" ON exam_slots (created_by);


CREATE INDEX IF NOT EXISTS "IX_exam_slots_proctor_id" ON exam_slots (proctor_id);


CREATE UNIQUE INDEX IF NOT EXISTS institutions_sub_domain_key ON institutions (sub_domain);


CREATE INDEX IF NOT EXISTS idx_notifications_user ON notifications (user_id);


CREATE INDEX IF NOT EXISTS "IX_pricing_configs_created_by" ON pricing_configs (created_by);


CREATE INDEX IF NOT EXISTS "IX_pricing_configs_updated_by" ON pricing_configs (updated_by);


CREATE INDEX IF NOT EXISTS idx_proctoring_settings_institution ON proctoring_settings (institution_id);


CREATE INDEX IF NOT EXISTS "IX_proctoring_settings_created_by" ON proctoring_settings (created_by);


CREATE INDEX IF NOT EXISTS "IX_proctoring_settings_updated_by" ON proctoring_settings (updated_by);


CREATE UNIQUE INDEX IF NOT EXISTS proctoring_violation_type_settings_unique ON proctoring_violation_type_settings (proctoring_settings_id, violation_type);


CREATE INDEX IF NOT EXISTS idx_question_options_question ON question_options (question_id);


CREATE INDEX IF NOT EXISTS idx_reading_passages_institution_name ON reading_passages (institution_id, exam_question_name);


CREATE INDEX IF NOT EXISTS idx_student_exam_records_exam_slot ON student_exam_records (exam_slot_id);


CREATE INDEX IF NOT EXISTS idx_student_exam_records_student ON student_exam_records (student_id);


CREATE INDEX IF NOT EXISTS idx_transactions_wallet ON transactions (wallet_id);


CREATE INDEX IF NOT EXISTS "IX_transactions_pricing_config_id" ON transactions (pricing_config_id);


CREATE UNIQUE INDEX IF NOT EXISTS transactions_vnpay_ref_key ON transactions (vnpay_ref);


CREATE INDEX IF NOT EXISTS idx_users_institution ON users (institution_id);


CREATE UNIQUE INDEX IF NOT EXISTS ux_student_code_per_institution ON users (institution_id, student_code) WHERE (student_code IS NOT NULL);


CREATE INDEX IF NOT EXISTS idx_violation_participation ON violation_logs (participation_id);


CREATE INDEX IF NOT EXISTS "IX_violation_logs_reviewed_by" ON violation_logs (reviewed_by);


CREATE UNIQUE INDEX IF NOT EXISTS wallets_institution_id_key ON wallets (institution_id);

-- =====================================================================
-- PART 2 — STORAGE BUCKETS (Supabase Storage)
-- All private: the backend uses the service-role key and serves files through
-- signed URLs (StorageService checks bucket visibility at runtime, so this is safe).
-- Adjust `public` to true only for a bucket you really want world-readable.
-- =====================================================================
INSERT INTO storage.buckets (id, name, public) VALUES
    ('biometric-faces', 'biometric-faces', false),
    ('attendance-snapshots', 'attendance-snapshots', false),
    ('attendance-videos', 'attendance-videos', false),
    ('exam-identity', 'exam-identity', false),
    ('exam-recordings', 'exam-recordings', false),
    ('exam-evidence', 'exam-evidence', false)
ON CONFLICT (id) DO NOTHING;


-- =====================================================================
-- PART 3 — LINK users.id TO Supabase Auth (auth.users.id)
-- The backend creates the auth user first, then inserts the app user with the SAME id,
-- and on delete removes the app user first, then the auth user (UserService.DeleteUserAsync).
-- NOT VALID: enforced for new/updated rows without failing on any pre-existing orphan row.
-- Run `ALTER TABLE users VALIDATE CONSTRAINT users_id_auth_fkey;` once you have confirmed
-- there are no orphans:  SELECT id FROM users WHERE id NOT IN (SELECT id FROM auth.users);
-- =====================================================================
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'users_id_auth_fkey') THEN
        ALTER TABLE public.users
            ADD CONSTRAINT users_id_auth_fkey
            FOREIGN KEY (id) REFERENCES auth.users (id) ON DELETE CASCADE NOT VALID;
    END IF;
END $$;

-- =====================================================================
-- PART 4 — ROW LEVEL SECURITY
-- The API never lets clients talk to the tables directly: every request goes through the
-- ASP.NET backend (Npgsql as the table owner / postgres role, Storage via service-role key),
-- and both bypass RLS. So: enable RLS on every table and define NO policies for
-- anon/authenticated => direct access via the public Supabase REST API is fully denied,
-- while the backend keeps working unchanged.
-- If you ever query tables straight from the frontend with the anon key, add policies first.
-- =====================================================================
ALTER TABLE public.contact_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.institutions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.reading_passages ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.wallets ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.exam_questions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.biometric_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.classes ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.notifications ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.pricing_configs ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.proctoring_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.question_options ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.biometric_data ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.class_enrollments ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.exam_slots ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.transactions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.proctoring_violation_type_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.student_exam_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.attendance_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.exam_participations ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.attendance_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.violation_logs ENABLE ROW LEVEL SECURITY;
