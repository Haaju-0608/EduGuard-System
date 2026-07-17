using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AttendanceRecord> AttendanceRecords { get; set; }

    public virtual DbSet<AttendanceSession> AttendanceSessions { get; set; }

    public virtual DbSet<BiometricDatum> BiometricData { get; set; }

    public virtual DbSet<BiometricRequest> BiometricRequests { get; set; }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<ClassEnrollment> ClassEnrollments { get; set; }

    public virtual DbSet<ExamParticipation> ExamParticipations { get; set; }

    public virtual DbSet<ExamQuestion> ExamQuestions { get; set; }

    public virtual DbSet<ExamSlot> ExamSlots { get; set; }

    public virtual DbSet<Institution> Institutions { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PricingConfig> PricingConfigs { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<ContactRequest> ContactRequests { get; set; }

    public virtual DbSet<StudentExamRecord> StudentExamRecords { get; set; }

    public virtual DbSet<ViolationLog> ViolationLogs { get; set; }

    public virtual DbSet<QuestionOption> QuestionOptions { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        //Để trống nha má không là lỗi hard code 0đ đóa
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum<AppRole>("app_role")
            .HasPostgresEnum<AttendanceMethod>("attendance_method")
            .HasPostgresEnum<AttendanceStatus>("attendance_status")
            .HasPostgresEnum("auth", "aal_level", new[] { "aal1", "aal2", "aal3" })
            .HasPostgresEnum("auth", "code_challenge_method", new[] { "s256", "plain" })
            .HasPostgresEnum("auth", "factor_status", new[] { "unverified", "verified" })
            .HasPostgresEnum("auth", "factor_type", new[] { "totp", "webauthn", "phone" })
            .HasPostgresEnum("auth", "oauth_authorization_status", new[] { "pending", "approved", "denied", "expired" })
            .HasPostgresEnum("auth", "oauth_client_type", new[] { "public", "confidential" })
            .HasPostgresEnum("auth", "oauth_registration_type", new[] { "dynamic", "manual" })
            .HasPostgresEnum("auth", "oauth_response_type", new[] { "code" })
            .HasPostgresEnum("auth", "one_time_token_type", new[] { "confirmation_token", "reauthentication_token", "recovery_token", "email_change_token_new", "email_change_token_current", "phone_change_token" })
            .HasPostgresEnum<BillingModel>("billing_model_enum")
            .HasPostgresEnum<BiometricReqStatus>("biometric_req_status")
            .HasPostgresEnum<EnrollmentStatus>("enrollment_status")
            .HasPostgresEnum<ExamSlotStatus>("exam_slot_status")
            .HasPostgresEnum<InstitutionStatus>("institution_status")
            .HasPostgresEnum<NotificationChannel>("notification_channel")
            .HasPostgresEnum<NotificationType>("notification_type")
            .HasPostgresEnum<ParticipationStatus>("participation_status")
            .HasPostgresEnum<PricingServiceType>("pricing_service_type")
            .HasPostgresEnum("realtime", "action", new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "ERROR" })
            .HasPostgresEnum("realtime", "equality_op", new[] { "eq", "neq", "lt", "lte", "gt", "gte", "in" })
            .HasPostgresEnum<ReferenceTypeEnum>("reference_type_enum")
            .HasPostgresEnum<SessionStatus>("session_status")
            .HasPostgresEnum<StudentExamRecordStatus>("student_exam_record_status")
            .HasPostgresEnum("storage", "buckettype", new[] { "STANDARD", "ANALYTICS", "VECTOR" })
            .HasPostgresEnum<TransactionStatus>("transaction_status")
            .HasPostgresEnum<TransactionType>("transaction_type")
            .HasPostgresEnum<UserStatus>("user_status")
            .HasPostgresEnum<ViolationSeverity>("violation_severity")
            .HasPostgresEnum<ViolationType>("violation_type")
            .HasPostgresExtension("extensions", "pg_stat_statements")
            .HasPostgresExtension("extensions", "pgcrypto")
            .HasPostgresExtension("extensions", "uuid-ossp")
            .HasPostgresExtension("vector")
            .HasPostgresExtension("vault", "supabase_vault");

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("attendance_records_pkey");

            entity.ToTable("attendance_records");

            entity.HasIndex(e => new { e.SessionId, e.StudentId }, "attendance_records_session_id_student_id_key").IsUnique();

            entity.HasIndex(e => e.StudentId, "idx_attendance_record_student");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AdjustedAt).HasColumnName("adjusted_at");
            entity.Property(e => e.AdjustedBy).HasColumnName("adjusted_by");
            entity.Property(e => e.CheckinAt).HasColumnName("checkin_at");
            entity.Property(e => e.ConfidenceScore).HasColumnName("confidence_score");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.SnapshotPath)
                .HasComment("Bucket: attendance-snapshots")
                .HasColumnName("snapshot_path");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.Method)
                .HasColumnName("method")
                .HasColumnType("attendance_method");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("attendance_status");

            entity.HasOne(d => d.AdjustedByNavigation).WithMany(p => p.AttendanceRecordAdjustedByNavigations)
                .HasForeignKey(d => d.AdjustedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("attendance_records_adjusted_by_fkey");

            entity.HasOne(d => d.Session).WithMany(p => p.AttendanceRecords)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("attendance_records_session_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.AttendanceRecordStudents)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("attendance_records_student_id_fkey");
        });

        modelBuilder.Entity<AttendanceSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("attendance_sessions_pkey");

            entity.ToTable("attendance_sessions");

            entity.HasIndex(e => e.BillingTransId, "attendance_sessions_billing_trans_id_key").IsUnique();

            entity.HasIndex(e => e.ClassId, "idx_attendance_session_class");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BillingTransId).HasColumnName("billing_trans_id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("session_status");
            entity.Property(e => e.TotalRecognized)
                .HasDefaultValue(0)
                .HasColumnName("total_recognized");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.VideoPath)
                .HasComment("Bucket: attendance-videos")
                .HasColumnName("video_path");

            entity.HasOne(d => d.BillingTrans).WithOne(p => p.AttendanceSession)
                .HasForeignKey<AttendanceSession>(d => d.BillingTransId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("attendance_sessions_billing_trans_id_fkey");

            entity.HasOne(d => d.Class).WithMany(p => p.AttendanceSessions)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("attendance_sessions_class_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.AttendanceSessions)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("attendance_sessions_created_by_fkey");
        });

        modelBuilder.Entity<BiometricDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("biometric_data_pkey");

            entity.ToTable("biometric_data");

            entity.HasIndex(e => e.FaceImageUrl, "idx_biometric_face_image");

            entity.HasIndex(e => e.UserId, "ux_biometric_active")
                .IsUnique()
                .HasFilter("(is_active = true)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.BioRequestId).HasColumnName("bio_request_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.Property(e => e.FaceVector)
      .HasColumnType("vector(128)")
      .HasColumnName("face_vector");

            entity.Property(e => e.FaceImageUrl)
                .HasComment("Bucket: biometric-faces")
                .HasColumnName("face_image_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.ModelVersion)
                .HasMaxLength(50)
                .HasComment("AI model version used to generate embedding")
                .HasColumnName("model_version");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.BioRequest).WithMany(p => p.BiometricData)
                .HasForeignKey(d => d.BioRequestId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("biometric_data_bio_request_id_fkey");

            entity.HasOne(d => d.User).WithOne(p => p.BiometricDatum)
                .HasForeignKey<BiometricDatum>(d => d.UserId)
                .HasConstraintName("biometric_data_user_id_fkey");
        });

        modelBuilder.Entity<BiometricRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("biometric_requests_pkey");

            entity.ToTable("biometric_requests");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ApprovedBy).HasColumnName("approved_by");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("biometric_req_status");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.FrontImagePath).HasColumnName("front_image_path");
            entity.Property(e => e.LeftImagePath).HasColumnName("left_image_path");
            entity.Property(e => e.RightImagePath).HasColumnName("right_image_path");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.BiometricRequestApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("biometric_requests_approved_by_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.BiometricRequestStudents)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("biometric_requests_student_id_fkey");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("classes_pkey");

            entity.ToTable("classes");

            entity.HasIndex(e => e.InstitutionId, "idx_classes_institution");

            entity.HasIndex(e => e.LecturerId, "idx_classes_lecturer");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AcademicYear)
                .HasMaxLength(20)
                .HasColumnName("academic_year");
            entity.Property(e => e.CourseCode)
                .HasMaxLength(50)
                .HasColumnName("course_code");
            entity.Property(e => e.CourseName)
                .HasMaxLength(255)
                .HasColumnName("course_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.InstitutionId).HasColumnName("institution_id");
            entity.Property(e => e.LecturerId).HasColumnName("lecturer_id");
            entity.Property(e => e.Semester)
                .HasMaxLength(50)
                .HasColumnName("semester");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ClassCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("classes_created_by_fkey");

            entity.HasOne(d => d.Institution).WithMany(p => p.Classes)
                .HasForeignKey(d => d.InstitutionId)
                .HasConstraintName("classes_institution_id_fkey");

            entity.HasOne(d => d.Lecturer).WithMany(p => p.ClassLecturers)
                .HasForeignKey(d => d.LecturerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("classes_lecturer_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ClassUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("classes_updated_by_fkey");
        });

        modelBuilder.Entity<ClassEnrollment>(entity =>
        {
            entity.HasKey(e => new { e.ClassId, e.StudentId }).HasName("class_enrollments_pkey");

            entity.ToTable("class_enrollments");

            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.EnrolledAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("enrolled_at");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("enrollment_status");

            entity.HasOne(d => d.Class).WithMany(p => p.ClassEnrollments)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("class_enrollments_class_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.ClassEnrollments)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("class_enrollments_student_id_fkey");
        });

        modelBuilder.Entity<ExamParticipation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("exam_participations_pkey");

            entity.ToTable("exam_participations");

            entity.HasIndex(e => e.BillingTransId, "exam_participations_billing_trans_id_key").IsUnique();

            entity.HasIndex(e => new { e.ExamSlotId, e.StudentId }, "exam_participations_exam_slot_id_student_id_key").IsUnique();

            entity.HasIndex(e => e.StudentId, "idx_exam_participation_student");

            entity.HasIndex(e => e.RecordingVideoPath, "idx_exam_recording");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ActualEnd).HasColumnName("actual_end");
            entity.Property(e => e.ActualStart).HasColumnName("actual_start");
            entity.Property(e => e.BillingTransId).HasColumnName("billing_trans_id");
            entity.Property(e => e.DisqualifiedReason).HasColumnName("disqualified_reason");
            entity.Property(e => e.ExamSlotId).HasColumnName("exam_slot_id");
            entity.Property(e => e.IdentitySnapshotPath)
                .HasComment("Bucket: exam-identity")
                .HasColumnName("identity_snapshot_path");
            entity.Property(e => e.RecordingVideoPath)
                .HasComment("Bucket: exam-recordings")
                .HasColumnName("recording_video_path");
            entity.Property(e => e.StudentId).HasColumnName("student_id");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("participation_status");

            entity.HasOne(d => d.BillingTrans).WithOne(p => p.ExamParticipation)
                .HasForeignKey<ExamParticipation>(d => d.BillingTransId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("exam_participations_billing_trans_id_fkey");

            entity.HasOne(d => d.ExamSlot).WithMany(p => p.ExamParticipations)
                .HasForeignKey(d => d.ExamSlotId)
                .HasConstraintName("exam_participations_exam_slot_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.ExamParticipations)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("exam_participations_student_id_fkey");
        });

        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("exam_questions_pkey");

            entity.ToTable("exam_questions");

            entity.HasIndex(e => e.ExamSlotId, "idx_exam_questions_exam_slot");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AudioUrl)
                .HasMaxLength(500)
                .HasColumnName("audio_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
            entity.Property(e => e.ExamSlotId).HasColumnName("exam_slot_id");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .HasColumnName("image_url");
            entity.Property(e => e.Points)
                .HasPrecision(6, 2)
                .HasDefaultValueSql("1")
                .HasColumnName("points");
            entity.Property(e => e.QuestionContent).HasColumnName("question_content");
            entity.Property(e => e.QuestionType)
                .HasMaxLength(30)
                .HasColumnName("question_type");

            entity.HasOne(d => d.ExamSlot).WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.ExamSlotId)
                .HasConstraintName("exam_questions_exam_slot_id_fkey");
        });

        modelBuilder.Entity<ExamSlot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("exam_slots_pkey");

            entity.ToTable("exam_slots");

            entity.HasIndex(e => e.ClassId, "idx_exam_slot_class");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.ExamName)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("exam_name");
            entity.Property(e => e.ExpectedDurationMinutes).HasColumnName("expected_duration_minutes");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("exam_slot_status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Class).WithMany(p => p.ExamSlots)
                .HasForeignKey(d => d.ClassId)
                .HasConstraintName("exam_slots_class_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ExamSlots)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("exam_slots_created_by_fkey");
        });

        modelBuilder.Entity<Institution>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("institutions_pkey");

            entity.ToTable("institutions");

            entity.HasIndex(e => e.SubDomain, "institutions_sub_domain_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(100)
                .HasColumnName("contact_email");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.SubDomain)
                .HasMaxLength(50)
                .HasColumnName("sub_domain");
            entity.Property(e => e.SubscriptionExpiresAt).HasColumnName("subscription_expires_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");

            entity.ToTable("notifications");

            entity.HasIndex(e => e.UserId, "idx_notifications_user");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.ReferenceType)
                .HasColumnName("reference_type")
                .HasColumnType("reference_type_enum");
            entity.Property(e => e.SentVia)
                .HasColumnName("sent_via")
                .HasColumnType("notification_channel");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasColumnType("notification_type");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("notifications_user_id_fkey");
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("question_options_pkey");

            entity.ToTable("question_options");

            entity.HasIndex(e => e.QuestionId, "idx_question_options_question");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.IsCorrect)
                .HasDefaultValue(false)
                .HasColumnName("is_correct");
            entity.Property(e => e.OptionContent).HasColumnName("option_content");
            entity.Property(e => e.OptionLabel)
                .HasMaxLength(10)
                .HasColumnName("option_label");
            entity.Property(e => e.QuestionId).HasColumnName("question_id");

            entity.HasOne(d => d.Question).WithMany(p => p.QuestionOptions)
                .HasForeignKey(d => d.QuestionId)
                .HasConstraintName("question_options_question_id_fkey");
        });

        modelBuilder.Entity<StudentExamRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("student_exam_records_pkey");

            entity.ToTable("student_exam_records");

            entity.HasIndex(e => e.ExamSlotId, "idx_student_exam_records_exam_slot");

            entity.HasIndex(e => e.StudentId, "idx_student_exam_records_student");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ExamSlotId).HasColumnName("exam_slot_id");
            entity.Property(e => e.StudentId)
                .HasComment("Must reference a users.id whose role is STUDENT.")
                .HasColumnName("student_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.ExamRecord)
                .HasColumnType("jsonb")
                .HasColumnName("exam_record");
            entity.Property(e => e.FinalScore)
                .HasPrecision(5, 2)
                .HasColumnName("final_score");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.DurationSeconds).HasColumnName("duration_seconds");
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasColumnType("student_exam_record_status")
                .HasDefaultValue(StudentExamRecordStatus.Marked);

            entity.HasOne(d => d.ExamSlot).WithMany(p => p.StudentExamRecords)
                .HasForeignKey(d => d.ExamSlotId)
                .HasConstraintName("student_exam_records_exam_slot_id_fkey");

            entity.HasOne(d => d.Student).WithMany(p => p.StudentExamRecords)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("student_exam_records_student_id_fkey");
        });

        modelBuilder.Entity<PricingConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pricing_configs_pkey");

            entity.ToTable("pricing_configs");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");

            // ================= SỬA LẠI ĐOẠN NÀY ĐỂ FIX TRIỆT ĐỂ LỖI TOÁN TỬ =================
            entity.Property(e => e.ServiceType)
                .HasColumnName("service_type")
                .HasColumnType("pricing_service_type"); // Định nghĩa chuẩn kiểu dữ liệu Enum của Postgres
                                                        // =================================================================================

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.EffectiveDate).HasColumnName("effective_date");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.Property(e => e.UnitPrice)
                .HasPrecision(18, 2)
                .HasComment("Pricing in VND")
                .HasColumnName("unit_price");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PricingConfigCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("pricing_configs_created_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PricingConfigUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("pricing_configs_updated_by_fkey");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transactions_pkey");

            entity.ToTable("transactions");

            entity.HasIndex(e => e.WalletId, "idx_transactions_wallet");

            entity.HasIndex(e => e.VnpayRef, "transactions_vnpay_ref_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.PricingConfigId).HasColumnName("pricing_config_id");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.VnpayRef)
                .HasMaxLength(100)
                .HasColumnName("vnpay_ref");
            entity.Property(e => e.WalletId).HasColumnName("wallet_id");

            entity.HasOne(d => d.PricingConfig).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.PricingConfigId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("transactions_pricing_config_id_fkey");

            entity.HasOne(d => d.Wallet).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.WalletId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("transactions_wallet_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.InstitutionId, "idx_users_institution");

            entity.HasIndex(e => new { e.InstitutionId, e.StudentCode }, "ux_student_code_per_institution")
                .IsUnique()
                .HasFilter("(student_code IS NOT NULL)");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.InstitutionId).HasColumnName("institution_id");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.StudentCode)
                .HasMaxLength(20)
                .HasColumnName("student_code");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Institution).WithMany(p => p.Users)
                .HasForeignKey(d => d.InstitutionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("users_institution_id_fkey");
        });

        modelBuilder.Entity<ViolationLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("violation_logs_pkey");

            entity.ToTable("violation_logs");

            entity.HasIndex(e => e.ParticipationId, "idx_violation_participation");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.AiConfidence).HasColumnName("ai_confidence");
            entity.Property(e => e.EvidencePath)
                .HasComment("Bucket: exam-evidence")
                .HasColumnName("evidence_path");
            entity.Property(e => e.IsReviewed)
                .HasDefaultValue(false)
                .HasColumnName("is_reviewed");
            entity.Property(e => e.ParticipationId).HasColumnName("participation_id");
            entity.Property(e => e.RecordedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("recorded_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");

            entity.HasOne(d => d.Participation).WithMany(p => p.ViolationLogs)
                .HasForeignKey(d => d.ParticipationId)
                .HasConstraintName("violation_logs_participation_id_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.ViolationLogs)
                .HasForeignKey(d => d.ReviewedBy)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("violation_logs_reviewed_by_fkey");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("wallets_pkey");

            entity.ToTable("wallets");

            entity.HasIndex(e => e.InstitutionId, "wallets_institution_id_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.Balance)
                .HasPrecision(18, 2)
                .HasColumnName("balance");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .HasDefaultValueSql("'VND'::character varying")
                .HasColumnName("currency");
            entity.Property(e => e.InstitutionId).HasColumnName("institution_id");
            entity.Property(e => e.LowBalanceAlertSentAt).HasColumnName("low_balance_alert_sent_at");
            entity.Property(e => e.LowBalanceThreshold)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("50000")
                .HasColumnName("low_balance_threshold");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Institution).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.InstitutionId)
                .HasConstraintName("wallets_institution_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
