-- Run on Supabase/Postgres before deploying: adds a per-exam-slot proctor assignment,
-- separate from the class's own lecturer, so a School Admin can assign a different
-- lecturer to proctor a specific exam without reassigning the whole class.

ALTER TABLE exam_slots
    ADD COLUMN IF NOT EXISTS proctor_id uuid REFERENCES users(id);

CREATE INDEX IF NOT EXISTS idx_exam_slots_proctor ON exam_slots(proctor_id);
