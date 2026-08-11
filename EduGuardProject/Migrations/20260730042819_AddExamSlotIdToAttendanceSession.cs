using System;
using EduGuardProject.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace EduGuardProject.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSlotIdToAttendanceSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:app_role.app_role", "STUDENT,LECTURER,SCHOOL_ADMIN,SUPER_ADMIN,student,lecturer,school_admin,super_admin")
                .Annotation("Npgsql:Enum:attendance_method.attendance_method", "AI,MANUAL")
                .Annotation("Npgsql:Enum:attendance_status.attendance_status", "PRESENT,ABSENT,LATE,EXCUSED")
                .Annotation("Npgsql:Enum:auth.aal_level", "aal1,aal2,aal3")
                .Annotation("Npgsql:Enum:auth.code_challenge_method", "s256,plain")
                .Annotation("Npgsql:Enum:auth.factor_status", "unverified,verified")
                .Annotation("Npgsql:Enum:auth.factor_type", "totp,webauthn,phone")
                .Annotation("Npgsql:Enum:auth.oauth_authorization_status", "pending,approved,denied,expired")
                .Annotation("Npgsql:Enum:auth.oauth_client_type", "public,confidential")
                .Annotation("Npgsql:Enum:auth.oauth_registration_type", "dynamic,manual")
                .Annotation("Npgsql:Enum:auth.oauth_response_type", "code")
                .Annotation("Npgsql:Enum:auth.one_time_token_type", "confirmation_token,reauthentication_token,recovery_token,email_change_token_new,email_change_token_current,phone_change_token")
                .Annotation("Npgsql:Enum:billing_model_enum.billing_model", "MONTHLY,YEARLY")
                .Annotation("Npgsql:Enum:biometric_req_status.biometric_req_status", "PENDING,APPROVED,REJECTED")
                .Annotation("Npgsql:Enum:enrollment_status.enrollment_status", "ACTIVE,DROPPED")
                .Annotation("Npgsql:Enum:exam_slot_status.exam_slot_status", "SCHEDULED,IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:institution_status.institution_status", "ACTIVE,INACTIVE,SUSPENDED")
                .Annotation("Npgsql:Enum:notification_channel.notification_channel", "PUSH,EMAIL,DASHBOARD")
                .Annotation("Npgsql:Enum:notification_type.notification_type", "LOW_BALANCE_ALERT,ATTENDANCE_SESSION_STARTED,EXAM_REMINDER,VIOLATION_DETECTED,BIOMETRIC_REQUEST_STATUS,SERVICE_SUSPENDED")
                .Annotation("Npgsql:Enum:participation_status.participation_status", "JOINED,SUBMITTED,DISQUALIFIED,ABSENT,LEFT")
                .Annotation("Npgsql:Enum:pricing_service_type.pricing_service_type", "attendance_unit,proctoring_per_hour")
                .Annotation("Npgsql:Enum:realtime.action", "INSERT,UPDATE,DELETE,TRUNCATE,ERROR")
                .Annotation("Npgsql:Enum:realtime.equality_op", "eq,neq,lt,lte,gt,gte,in")
                .Annotation("Npgsql:Enum:reference_type_enum.reference_type_enum", "INSTITUTION,ATTENDANCE_SESSION,EXAM_SLOT,TRANSACTION")
                .Annotation("Npgsql:Enum:session_status.session_status", "IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:storage.buckettype", "STANDARD,ANALYTICS,VECTOR")
                .Annotation("Npgsql:Enum:student_exam_record_status.student_exam_record_status", "MARKED,COMPLETED,DELETED")
                .Annotation("Npgsql:Enum:transaction_status.transaction_status", "PENDING,SUCCESS,FAILED,pending,success,failed")
                .Annotation("Npgsql:Enum:transaction_type.transaction_type", "TOP_UP,ATTENDANCE_FEE,PROCTORING_FEE,top_up,attendance_fee,proctoring_fee")
                .Annotation("Npgsql:Enum:user_status.user_status", "ACTIVE,BLOCKED")
                .Annotation("Npgsql:Enum:violation_severity.violation_severity", "WARNING,SEVERE")
                .Annotation("Npgsql:Enum:violation_type.violation_type", "IMPERSONATION,GAZE_DIVERSION,MULTIPLE_FACES,ABSENCE,HEAD_TURN,FACE_OBSTRUCTED,TabSwitch,WindowBlur,ExitFullscreen")
                .Annotation("Npgsql:PostgresExtension:extensions.pg_stat_statements", ",,")
                .Annotation("Npgsql:PostgresExtension:extensions.pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:extensions.uuid-ossp", ",,")
                .Annotation("Npgsql:PostgresExtension:vault.supabase_vault", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "contact_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_name = table.Column<string>(type: "text", nullable: false),
                    contact_person_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "institutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sub_domain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subscription_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    billing_model = table.Column<BillingModel>(type: "billing_model_enum", nullable: false),
                    status = table.Column<InstitutionStatus>(type: "institution_status", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("institutions_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<UserStatus>(type: "user_status", nullable: false),
                    role = table.Column<AppRole>(type: "app_role", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                    table.ForeignKey(
                        name: "users_institution_id_fkey",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValueSql: "'VND'::character varying"),
                    low_balance_threshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValueSql: "50000"),
                    low_balance_alert_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("wallets_pkey", x => x.id);
                    table.ForeignKey(
                        name: "wallets_institution_id_fkey",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "biometric_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<BiometricReqStatus>(type: "biometric_req_status", nullable: false),
                    front_image_path = table.Column<string>(type: "text", nullable: true),
                    left_image_path = table.Column<string>(type: "text", nullable: true),
                    right_image_path = table.Column<string>(type: "text", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("biometric_requests_pkey", x => x.id);
                    table.ForeignKey(
                        name: "biometric_requests_approved_by_fkey",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "biometric_requests_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "classes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    institution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    course_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    semester = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    academic_year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("classes_pkey", x => x.id);
                    table.ForeignKey(
                        name: "classes_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "classes_institution_id_fkey",
                        column: x => x.institution_id,
                        principalTable: "institutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "classes_lecturer_id_fkey",
                        column: x => x.lecturer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "classes_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<NotificationType>(type: "notification_type", nullable: false),
                    reference_type = table.Column<ReferenceTypeEnum>(type: "reference_type_enum", nullable: true),
                    sent_via = table.Column<NotificationChannel>(type: "notification_channel", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("notifications_pkey", x => x.id);
                    table.ForeignKey(
                        name: "notifications_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pricing_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_type = table.Column<PricingServiceType>(type: "pricing_service_type", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, comment: "Pricing in VND"),
                    effective_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pricing_configs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "pricing_configs_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "pricing_configs_updated_by_fkey",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "biometric_data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bio_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    face_vector = table.Column<Vector>(type: "vector(128)", nullable: true),
                    model_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "AI model version used to generate embedding"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    face_image_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "Bucket: biometric-faces")
                },
                constraints: table =>
                {
                    table.PrimaryKey("biometric_data_pkey", x => x.id);
                    table.ForeignKey(
                        name: "biometric_data_bio_request_id_fkey",
                        column: x => x.bio_request_id,
                        principalTable: "biometric_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "biometric_data_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_enrollments",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<EnrollmentStatus>(type: "enrollment_status", nullable: false),
                    enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_enrollments_pkey", x => new { x.class_id, x.student_id });
                    table.ForeignKey(
                        name: "class_enrollments_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "class_enrollments_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expected_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<ExamSlotStatus>(type: "exam_slot_status", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("exam_slots_pkey", x => x.id);
                    table.ForeignKey(
                        name: "exam_slots_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "exam_slots_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pricing_config_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vnpay_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    type = table.Column<TransactionType>(type: "transaction_type", nullable: false),
                    status = table.Column<TransactionStatus>(type: "transaction_status", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("transactions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "transactions_pricing_config_id_fkey",
                        column: x => x.pricing_config_id,
                        principalTable: "pricing_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "transactions_wallet_id_fkey",
                        column: x => x.wallet_id,
                        principalTable: "wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    question_content = table.Column<string>(type: "text", nullable: false),
                    audio_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    points = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValueSql: "1"),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("exam_questions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "exam_questions_exam_slot_id_fkey",
                        column: x => x.exam_slot_id,
                        principalTable: "exam_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_exam_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Must reference a users.id whose role is STUDENT."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exam_record = table.Column<string>(type: "jsonb", nullable: true),
                    final_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<StudentExamRecordStatus>(type: "student_exam_record_status", nullable: false, defaultValue: StudentExamRecordStatus.Marked)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_exam_records_pkey", x => x.id);
                    table.ForeignKey(
                        name: "student_exam_records_exam_slot_id_fkey",
                        column: x => x.exam_slot_id,
                        principalTable: "exam_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "student_exam_records_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamSlotId = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_trans_id = table.Column<Guid>(type: "uuid", nullable: true),
                    video_path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "Bucket: attendance-videos"),
                    total_recognized = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<SessionStatus>(type: "session_status", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("attendance_sessions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_sessions_exam_slots_ExamSlotId",
                        column: x => x.ExamSlotId,
                        principalTable: "exam_slots",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "attendance_sessions_billing_trans_id_fkey",
                        column: x => x.billing_trans_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "attendance_sessions_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "attendance_sessions_created_by_fkey",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_participations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_trans_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actual_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actual_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<ParticipationStatus>(type: "participation_status", nullable: false),
                    disqualified_reason = table.Column<string>(type: "text", nullable: true),
                    recording_video_path = table.Column<string>(type: "text", nullable: true, comment: "Bucket: exam-recordings"),
                    identity_snapshot_path = table.Column<string>(type: "text", nullable: true, comment: "Bucket: exam-identity")
                },
                constraints: table =>
                {
                    table.PrimaryKey("exam_participations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "exam_participations_billing_trans_id_fkey",
                        column: x => x.billing_trans_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "exam_participations_exam_slot_id_fkey",
                        column: x => x.exam_slot_id,
                        principalTable: "exam_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "exam_participations_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_label = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    option_content = table.Column<string>(type: "text", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("question_options_pkey", x => x.id);
                    table.ForeignKey(
                        name: "question_options_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "exam_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confidence_score = table.Column<double>(type: "double precision", nullable: true),
                    snapshot_path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "Bucket: attendance-snapshots"),
                    checkin_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    adjusted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    adjusted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    method = table.Column<AttendanceMethod>(type: "attendance_method", nullable: false),
                    status = table.Column<AttendanceStatus>(type: "attendance_status", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("attendance_records_pkey", x => x.id);
                    table.ForeignKey(
                        name: "attendance_records_adjusted_by_fkey",
                        column: x => x.adjusted_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "attendance_records_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "attendance_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "attendance_records_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "violation_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    participation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_path = table.Column<string>(type: "text", nullable: true, comment: "Bucket: exam-evidence"),
                    severity = table.Column<ViolationSeverity>(type: "violation_severity", nullable: false),
                    violation_type = table.Column<ViolationType>(type: "violation_type", nullable: false),
                    ai_confidence = table.Column<double>(type: "double precision", nullable: true),
                    is_reviewed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("violation_logs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "violation_logs_participation_id_fkey",
                        column: x => x.participation_id,
                        principalTable: "exam_participations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "violation_logs_reviewed_by_fkey",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "attendance_records_session_id_student_id_key",
                table: "attendance_records",
                columns: new[] { "session_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_attendance_record_student",
                table: "attendance_records",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_adjusted_by",
                table: "attendance_records",
                column: "adjusted_by");

            migrationBuilder.CreateIndex(
                name: "attendance_sessions_billing_trans_id_key",
                table: "attendance_sessions",
                column: "billing_trans_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_attendance_session_class",
                table: "attendance_sessions",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_sessions_created_by",
                table: "attendance_sessions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_sessions_ExamSlotId",
                table: "attendance_sessions",
                column: "ExamSlotId");

            migrationBuilder.CreateIndex(
                name: "idx_biometric_face_image",
                table: "biometric_data",
                column: "face_image_url");

            migrationBuilder.CreateIndex(
                name: "IX_biometric_data_bio_request_id",
                table: "biometric_data",
                column: "bio_request_id");

            migrationBuilder.CreateIndex(
                name: "ux_biometric_active",
                table: "biometric_data",
                column: "user_id",
                unique: true,
                filter: "(is_active = true)");

            migrationBuilder.CreateIndex(
                name: "IX_biometric_requests_approved_by",
                table: "biometric_requests",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_biometric_requests_student_id",
                table: "biometric_requests",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_enrollments_student_id",
                table: "class_enrollments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "idx_classes_institution",
                table: "classes",
                column: "institution_id");

            migrationBuilder.CreateIndex(
                name: "idx_classes_lecturer",
                table: "classes",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_created_by",
                table: "classes",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_classes_updated_by",
                table: "classes",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "exam_participations_billing_trans_id_key",
                table: "exam_participations",
                column: "billing_trans_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "exam_participations_exam_slot_id_student_id_key",
                table: "exam_participations",
                columns: new[] { "exam_slot_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_exam_participation_student",
                table: "exam_participations",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "idx_exam_recording",
                table: "exam_participations",
                column: "recording_video_path");

            migrationBuilder.CreateIndex(
                name: "idx_exam_questions_exam_slot",
                table: "exam_questions",
                column: "exam_slot_id");

            migrationBuilder.CreateIndex(
                name: "idx_exam_slot_class",
                table: "exam_slots",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_exam_slots_created_by",
                table: "exam_slots",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "institutions_sub_domain_key",
                table: "institutions",
                column: "sub_domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_notifications_user",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_configs_created_by",
                table: "pricing_configs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_configs_updated_by",
                table: "pricing_configs",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "idx_question_options_question",
                table: "question_options",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "idx_student_exam_records_exam_slot",
                table: "student_exam_records",
                column: "exam_slot_id");

            migrationBuilder.CreateIndex(
                name: "idx_student_exam_records_student",
                table: "student_exam_records",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_wallet",
                table: "transactions",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_pricing_config_id",
                table: "transactions",
                column: "pricing_config_id");

            migrationBuilder.CreateIndex(
                name: "transactions_vnpay_ref_key",
                table: "transactions",
                column: "vnpay_ref",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_users_institution",
                table: "users",
                column: "institution_id");

            migrationBuilder.CreateIndex(
                name: "ux_student_code_per_institution",
                table: "users",
                columns: new[] { "institution_id", "student_code" },
                unique: true,
                filter: "(student_code IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "idx_violation_participation",
                table: "violation_logs",
                column: "participation_id");

            migrationBuilder.CreateIndex(
                name: "IX_violation_logs_reviewed_by",
                table: "violation_logs",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "wallets_institution_id_key",
                table: "wallets",
                column: "institution_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_records");

            migrationBuilder.DropTable(
                name: "biometric_data");

            migrationBuilder.DropTable(
                name: "class_enrollments");

            migrationBuilder.DropTable(
                name: "contact_requests");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "question_options");

            migrationBuilder.DropTable(
                name: "student_exam_records");

            migrationBuilder.DropTable(
                name: "violation_logs");

            migrationBuilder.DropTable(
                name: "attendance_sessions");

            migrationBuilder.DropTable(
                name: "biometric_requests");

            migrationBuilder.DropTable(
                name: "exam_questions");

            migrationBuilder.DropTable(
                name: "exam_participations");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "exam_slots");

            migrationBuilder.DropTable(
                name: "pricing_configs");

            migrationBuilder.DropTable(
                name: "wallets");

            migrationBuilder.DropTable(
                name: "classes");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "institutions");
        }
    }
}
