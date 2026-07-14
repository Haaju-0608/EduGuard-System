DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_type
        WHERE typname = 'student_exam_record_status'
    ) THEN
        CREATE TYPE student_exam_record_status AS ENUM ('COMPLETED', 'MARKED', 'DELETED');
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS student_exam_records (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    exam_slot_id uuid NOT NULL REFERENCES exam_slots(id),
    student_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    ended_at timestamp with time zone NULL,
    exam_record text NULL,
    status student_exam_record_status NOT NULL DEFAULT 'MARKED'
);

ALTER TABLE IF EXISTS student_exam_records
    ADD COLUMN IF NOT EXISTS exam_record text NULL;

CREATE INDEX IF NOT EXISTS idx_student_exam_records_exam_slot
    ON student_exam_records(exam_slot_id);

CREATE INDEX IF NOT EXISTS idx_student_exam_records_student
    ON student_exam_records(student_id);

CREATE OR REPLACE FUNCTION ensure_student_exam_record_student()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM users
        WHERE id = NEW.student_id
          AND role = 'STUDENT'::app_role
    ) THEN
        RAISE EXCEPTION 'student_id must reference a user with role STUDENT';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_student_exam_records_student_role
    ON student_exam_records;

CREATE TRIGGER trg_student_exam_records_student_role
BEFORE INSERT OR UPDATE OF student_id
ON student_exam_records
FOR EACH ROW
EXECUTE FUNCTION ensure_student_exam_record_student();
