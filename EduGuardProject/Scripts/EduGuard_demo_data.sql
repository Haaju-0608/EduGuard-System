-- =====================================================================
-- EduGuard - DEMO DATA (run AFTER EduGuard_full_schema.sql)
-- Creates one demo institution with accounts for every role, a class, an exam
-- (questions + participations + violations), attendance and wallet data.
-- Every demo account shares the password:  Demo@123456
-- Safe to re-run (ON CONFLICT DO NOTHING). Fixed UUIDs make rows easy to find/remove.
-- NOT included: face vectors (biometric_data) - they are produced by the AI service;
-- register faces through the app (Student -> biometric request -> approve).
-- =====================================================================

-- 1. Institution + wallet + pricing
INSERT INTO institutions (id, name, sub_domain, contact_email, subscription_expires_at, billing_model, status)
VALUES ('d0000000-0000-4000-8000-000000000001', 'EduGuard Demo University', 'demo', 'contact@eduguard.demo', now() + interval '1 year', 'MONTHLY', 'ACTIVE')
ON CONFLICT (id) DO NOTHING;

INSERT INTO wallets (id, institution_id, balance) VALUES
 ('d1000000-0000-4000-8000-000000000001', 'd0000000-0000-4000-8000-000000000001', 5000000)
ON CONFLICT (id) DO NOTHING;

INSERT INTO pricing_configs (id, service_type, unit_price, effective_date, is_active) VALUES
 ('d2000000-0000-4000-8000-000000000001','attendance_unit',1000, now(), true),
 ('d2000000-0000-4000-8000-000000000002','proctoring_per_hour',20000, now(), true),
 ('d2000000-0000-4000-8000-000000000003','subscription_monthly',500000, now(), true),
 ('d2000000-0000-4000-8000-000000000004','subscription_yearly',5000000, now(), true)
ON CONFLICT (id) DO NOTHING;

-- 2. Supabase Auth accounts (so login works) - password: Demo@123456
INSERT INTO auth.users (instance_id, id, aud, role, email, encrypted_password, email_confirmed_at,
    raw_app_meta_data, raw_user_meta_data, created_at, updated_at,
    confirmation_token, recovery_token, email_change_token_new, email_change)
VALUES
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000001','authenticated','authenticated','superadmin@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','',''),
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000002','authenticated','authenticated','schooladmin@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','',''),
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000003','authenticated','authenticated','lecturer1@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','',''),
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000004','authenticated','authenticated','lecturer2@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','',''),
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000005','authenticated','authenticated','student1@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','',''),
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000006','authenticated','authenticated','student2@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','',''),
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000007','authenticated','authenticated','student3@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','',''),
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000008','authenticated','authenticated','student4@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','',''),
('00000000-0000-0000-0000-000000000000','a0000000-0000-4000-8000-000000000009','authenticated','authenticated','student5@eduguard.demo',extensions.crypt('Demo@123456', extensions.gen_salt('bf')),now(),'{"provider":"email","providers":["email"]}','{"email_verified":true}',now(),now(),'','','','')
ON CONFLICT (id) DO NOTHING;

INSERT INTO auth.identities (id, user_id, provider_id, identity_data, provider, last_sign_in_at, created_at, updated_at)
VALUES
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000001','{"sub":"a0000000-0000-4000-8000-000000000001","email":"superadmin@eduguard.demo","email_verified":true}','email',now(),now(),now()),
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000002','{"sub":"a0000000-0000-4000-8000-000000000002","email":"schooladmin@eduguard.demo","email_verified":true}','email',now(),now(),now()),
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000003','a0000000-0000-4000-8000-000000000003','{"sub":"a0000000-0000-4000-8000-000000000003","email":"lecturer1@eduguard.demo","email_verified":true}','email',now(),now(),now()),
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000004','a0000000-0000-4000-8000-000000000004','{"sub":"a0000000-0000-4000-8000-000000000004","email":"lecturer2@eduguard.demo","email_verified":true}','email',now(),now(),now()),
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000005','a0000000-0000-4000-8000-000000000005','{"sub":"a0000000-0000-4000-8000-000000000005","email":"student1@eduguard.demo","email_verified":true}','email',now(),now(),now()),
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000006','a0000000-0000-4000-8000-000000000006','{"sub":"a0000000-0000-4000-8000-000000000006","email":"student2@eduguard.demo","email_verified":true}','email',now(),now(),now()),
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000007','a0000000-0000-4000-8000-000000000007','{"sub":"a0000000-0000-4000-8000-000000000007","email":"student3@eduguard.demo","email_verified":true}','email',now(),now(),now()),
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000008','a0000000-0000-4000-8000-000000000008','{"sub":"a0000000-0000-4000-8000-000000000008","email":"student4@eduguard.demo","email_verified":true}','email',now(),now(),now()),
(gen_random_uuid(),'a0000000-0000-4000-8000-000000000009','a0000000-0000-4000-8000-000000000009','{"sub":"a0000000-0000-4000-8000-000000000009","email":"student5@eduguard.demo","email_verified":true}','email',now(),now(),now())
ON CONFLICT (provider_id, provider) DO NOTHING;

-- 3. Application users (same ids as auth.users)
INSERT INTO users (id, institution_id, student_code, email, full_name, phone, status, role) VALUES
('a0000000-0000-4000-8000-000000000001',NULL,NULL,'superadmin@eduguard.demo','Demo Super Admin','0900000000','ACTIVE','SUPER_ADMIN'),
('a0000000-0000-4000-8000-000000000002','d0000000-0000-4000-8000-000000000001',NULL,'schooladmin@eduguard.demo','Demo School Admin','0900000001','ACTIVE','SCHOOL_ADMIN'),
('a0000000-0000-4000-8000-000000000003','d0000000-0000-4000-8000-000000000001',NULL,'lecturer1@eduguard.demo','Nguyen Van Giang Vien','0900000002','ACTIVE','LECTURER'),
('a0000000-0000-4000-8000-000000000004','d0000000-0000-4000-8000-000000000001',NULL,'lecturer2@eduguard.demo','Tran Thi Giang Vien','0900000003','ACTIVE','LECTURER'),
('a0000000-0000-4000-8000-000000000005','d0000000-0000-4000-8000-000000000001','SE000001','student1@eduguard.demo','Le Minh An','0900000004','ACTIVE','STUDENT'),
('a0000000-0000-4000-8000-000000000006','d0000000-0000-4000-8000-000000000001','SE000002','student2@eduguard.demo','Pham Quoc Bao','0900000005','ACTIVE','STUDENT'),
('a0000000-0000-4000-8000-000000000007','d0000000-0000-4000-8000-000000000001','SE000003','student3@eduguard.demo','Vo Thanh Chi','0900000006','ACTIVE','STUDENT'),
('a0000000-0000-4000-8000-000000000008','d0000000-0000-4000-8000-000000000001','SE000004','student4@eduguard.demo','Do Gia Dung','0900000007','ACTIVE','STUDENT'),
('a0000000-0000-4000-8000-000000000009','d0000000-0000-4000-8000-000000000001','SE000005','student5@eduguard.demo','Hoang Ngoc Em','0900000008','ACTIVE','STUDENT')
ON CONFLICT (id) DO NOTHING;

-- 4. Classes + enrollments (5 students in class 1, first 3 in class 2)
INSERT INTO classes (id, institution_id, lecturer_id, course_name, course_code, semester, academic_year, start_date, end_date, created_by) VALUES
 ('c0000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000003','Lap trinh Web','PRN232','Fall','2026', current_date - 60, current_date + 60,'a0000000-0000-4000-8000-000000000002'),
 ('c0000000-0000-4000-8000-000000000002','d0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000004','Co so du lieu','DBI202','Fall','2026', current_date - 60, current_date + 60,'a0000000-0000-4000-8000-000000000002')
ON CONFLICT (id) DO NOTHING;

INSERT INTO class_enrollments (class_id, student_id, status) VALUES
('c0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000005','ACTIVE'),
('c0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000006','ACTIVE'),
('c0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000007','ACTIVE'),
('c0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000008','ACTIVE'),
('c0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000009','ACTIVE'),
('c0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000005','ACTIVE'),
('c0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000006','ACTIVE'),
('c0000000-0000-4000-8000-000000000002','a0000000-0000-4000-8000-000000000007','ACTIVE')
ON CONFLICT DO NOTHING;

-- 5. Question bank (institution level) - "Web Fundamentals Quiz"
INSERT INTO exam_questions (id, institution_id, exam_question_name, question_type, question_content, points, display_order) VALUES
 ('f0000000-0000-4000-8000-000000000001','d0000000-0000-4000-8000-000000000001','Web Fundamentals Quiz','MultipleChoice','HTTP status code 404 means?',5,1),
 ('f0000000-0000-4000-8000-000000000002','d0000000-0000-4000-8000-000000000001','Web Fundamentals Quiz','MultipleChoice','Which HTML tag defines a hyperlink?',5,2)
ON CONFLICT (id) DO NOTHING;

INSERT INTO question_options (id, question_id, option_label, option_content, is_correct) VALUES
 ('f1000000-0000-4000-8000-000000000001','f0000000-0000-4000-8000-000000000001','A','Not Found',true),
 ('f1000000-0000-4000-8000-000000000002','f0000000-0000-4000-8000-000000000001','B','Server Error',false),
 ('f1000000-0000-4000-8000-000000000003','f0000000-0000-4000-8000-000000000001','C','Forbidden',false),
 ('f1000000-0000-4000-8000-000000000004','f0000000-0000-4000-8000-000000000002','A','<a>',true),
 ('f1000000-0000-4000-8000-000000000005','f0000000-0000-4000-8000-000000000002','B','<link>',false),
 ('f1000000-0000-4000-8000-000000000006','f0000000-0000-4000-8000-000000000002','C','<href>',false)
ON CONFLICT (id) DO NOTHING;

-- 6. Exam slots: one finished (with results/violations), one upcoming
INSERT INTO exam_slots (id, class_id, created_by, exam_name, exam_question_name, start_time, end_time, expected_duration_minutes, status, proctor_id) VALUES
 ('e0000000-0000-4000-8000-000000000001','c0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000003','Midterm - Web Fundamentals','Web Fundamentals Quiz', now() - interval '7 days', now() - interval '7 days' + interval '2 hours', 30, 'COMPLETED','a0000000-0000-4000-8000-000000000003'),
 ('e0000000-0000-4000-8000-000000000002','c0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000003','Final - Web Fundamentals','Web Fundamentals Quiz', now() + interval '3 days', now() + interval '3 days' + interval '2 hours', 30, 'SCHEDULED','a0000000-0000-4000-8000-000000000003')
ON CONFLICT (id) DO NOTHING;

-- 7. Participations of the finished exam (proctoring rule snapshot = system defaults)
INSERT INTO exam_participations (id, exam_slot_id, student_id, actual_start, actual_end, status, disqualified_reason,
    max_ai_violation_count_snapshot, cooldown_seconds_snapshot, allow_consecutive_same_type_snapshot, ai_notify_threshold_snapshot, browser_notify_threshold_snapshot) VALUES
 ('b0000000-0000-4000-8000-000000000001','e0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000005', now() - interval '7 days' + interval '2 minutes', now() - interval '7 days' + interval '25 minutes','SUBMITTED',NULL,10,5,false,3,3),
 ('b0000000-0000-4000-8000-000000000002','e0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000006', now() - interval '7 days' + interval '2 minutes', now() - interval '7 days' + interval '20 minutes','DISQUALIFIED','Disqualified by lecturer after repeated violations',10,5,false,3,3),
 ('b0000000-0000-4000-8000-000000000003','e0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000007', now() - interval '7 days' + interval '3 minutes', now() - interval '7 days' + interval '28 minutes','SUBMITTED',NULL,10,5,false,3,3),
 ('b0000000-0000-4000-8000-000000000004','e0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000008', NULL, NULL,'ABSENT',NULL,10,5,false,3,3)
ON CONFLICT (id) DO NOTHING;

INSERT INTO student_exam_records (id, exam_slot_id, student_id, created_at, ended_at, final_score, submitted_at, duration_seconds, status) VALUES
 ('b1000000-0000-4000-8000-000000000001','e0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000005', now() - interval '7 days' + interval '2 minutes', now() - interval '7 days' + interval '25 minutes', 10, now() - interval '7 days' + interval '25 minutes', 1380,'COMPLETED'),
 ('b1000000-0000-4000-8000-000000000003','e0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000007', now() - interval '7 days' + interval '3 minutes', now() - interval '7 days' + interval '28 minutes', 5, now() - interval '7 days' + interval '28 minutes', 1500,'COMPLETED')
ON CONFLICT (id) DO NOTHING;

-- 8. Violations (student 2: 3 AI + 3 browser violations -> disqualified by lecturer)
INSERT INTO violation_logs (id, participation_id, severity, violation_type, ai_confidence, is_reviewed, recorded_at) VALUES
 ('b2000000-0000-4000-8000-000000000001','b0000000-0000-4000-8000-000000000002','WARNING','GAZE_DIVERSION',0.82,true, now() - interval '7 days' + interval '6 minutes'),
 ('b2000000-0000-4000-8000-000000000002','b0000000-0000-4000-8000-000000000002','WARNING','HEAD_TURN',0.77,true, now() - interval '7 days' + interval '9 minutes'),
 ('b2000000-0000-4000-8000-000000000003','b0000000-0000-4000-8000-000000000002','SEVERE','MULTIPLE_FACES',0.91,true, now() - interval '7 days' + interval '13 minutes'),
 ('b2000000-0000-4000-8000-000000000004','b0000000-0000-4000-8000-000000000002','WARNING','TabSwitch',NULL,true, now() - interval '7 days' + interval '15 minutes'),
 ('b2000000-0000-4000-8000-000000000005','b0000000-0000-4000-8000-000000000002','WARNING','WindowBlur',NULL,true, now() - interval '7 days' + interval '17 minutes'),
 ('b2000000-0000-4000-8000-000000000006','b0000000-0000-4000-8000-000000000002','WARNING','ExitFullscreen',NULL,true, now() - interval '7 days' + interval '18 minutes'),
 ('b2000000-0000-4000-8000-000000000007','b0000000-0000-4000-8000-000000000003','WARNING','GAZE_DIVERSION',0.64,false, now() - interval '7 days' + interval '10 minutes')
ON CONFLICT (id) DO NOTHING;

-- 9. Attendance session (class 1) with check-ins
INSERT INTO attendance_sessions (id, class_id, created_by, total_recognized, start_time, end_time, status) VALUES
 ('a1000000-0000-4000-8000-000000000001','c0000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000003',4, now() - interval '2 days', now() - interval '2 days' + interval '1 hour','COMPLETED')
ON CONFLICT (id) DO NOTHING;

INSERT INTO attendance_records (id, session_id, student_id, confidence_score, checkin_at, method, status) VALUES
 ('a2000000-0000-4000-8000-000000000001','a1000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000005',0.97, now() - interval '2 days' + interval '3 minutes','AI','PRESENT'),
 ('a2000000-0000-4000-8000-000000000002','a1000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000006',0.95, now() - interval '2 days' + interval '4 minutes','AI','PRESENT'),
 ('a2000000-0000-4000-8000-000000000003','a1000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000007',0.93, now() - interval '2 days' + interval '9 minutes','AI','LATE'),
 ('a2000000-0000-4000-8000-000000000004','a1000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000008',0.91, now() - interval '2 days' + interval '5 minutes','AI','PRESENT'),
 ('a2000000-0000-4000-8000-000000000005','a1000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000009',NULL, NULL,'MANUAL','ABSENT')
ON CONFLICT (id) DO NOTHING;

-- 10. Wallet transactions + a notification
INSERT INTO transactions (id, wallet_id, amount, type, status, description, processed_at) VALUES
 ('d3000000-0000-4000-8000-000000000001','d1000000-0000-4000-8000-000000000001',5000000,'TOP_UP','SUCCESS','Demo top-up', now() - interval '10 days'),
 ('d3000000-0000-4000-8000-000000000002','d1000000-0000-4000-8000-000000000001',60000,'PROCTORING_FEE','SUCCESS','Proctoring fee - Midterm', now() - interval '7 days')
ON CONFLICT (id) DO NOTHING;

INSERT INTO notifications (id, user_id, title, body, type, reference_type, reference_id, sent_via) VALUES
 ('d4000000-0000-4000-8000-000000000001','a0000000-0000-4000-8000-000000000003','Sinh vien dat nguong canh bao vi pham','Sinh vien Pham Quoc Bao da dat 3 vi pham AI. Vui long xem xet.','VIOLATION_DETECTED','EXAM_PARTICIPATION','b0000000-0000-4000-8000-000000000002','DASHBOARD')
ON CONFLICT (id) DO NOTHING;
