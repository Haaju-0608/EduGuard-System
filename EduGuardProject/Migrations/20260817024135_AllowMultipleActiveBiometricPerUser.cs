using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduGuardProject.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleActiveBiometricPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            

            migrationBuilder.DropIndex(
                name: "ux_biometric_active",
                table: "biometric_data");

           

           

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
                .Annotation("Npgsql:Enum:pricing_service_type.pricing_service_type", "attendance_unit,proctoring_per_hour,subscription_monthly,subscription_yearly")
                .Annotation("Npgsql:Enum:realtime.action", "INSERT,UPDATE,DELETE,TRUNCATE,ERROR")
                .Annotation("Npgsql:Enum:realtime.equality_op", "eq,neq,lt,lte,gt,gte,in")
                .Annotation("Npgsql:Enum:reference_type_enum.reference_type_enum", "INSTITUTION,ATTENDANCE_SESSION,EXAM_SLOT,TRANSACTION")
                .Annotation("Npgsql:Enum:session_status.session_status", "IN_PROGRESS,COMPLETED,CANCELLED")
                .Annotation("Npgsql:Enum:storage.buckettype", "STANDARD,ANALYTICS,VECTOR")
                .Annotation("Npgsql:Enum:student_exam_record_status.student_exam_record_status", "MARKED,COMPLETED,DELETED")
                .Annotation("Npgsql:Enum:transaction_status.transaction_status", "PENDING,SUCCESS,FAILED,pending,success,failed")
                .Annotation("Npgsql:Enum:transaction_type.transaction_type", "TOP_UP,ATTENDANCE_FEE,PROCTORING_FEE,top_up,attendance_fee,proctoring_fee,SUBSCRIPTION_FEE")
                .Annotation("Npgsql:Enum:user_status.user_status", "ACTIVE,BLOCKED")
                .Annotation("Npgsql:Enum:violation_severity.violation_severity", "WARNING,SEVERE")
                .Annotation("Npgsql:Enum:violation_type.violation_type", "IMPERSONATION,GAZE_DIVERSION,MULTIPLE_FACES,ABSENCE,HEAD_TURN,FACE_OBSTRUCTED,TabSwitch,WindowBlur,ExitFullscreen")
                .Annotation("Npgsql:PostgresExtension:extensions.pg_stat_statements", ",,")
                .Annotation("Npgsql:PostgresExtension:extensions.pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:extensions.uuid-ossp", ",,")
                .Annotation("Npgsql:PostgresExtension:vault.supabase_vault", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:app_role.app_role", "STUDENT,LECTURER,SCHOOL_ADMIN,SUPER_ADMIN,student,lecturer,school_admin,super_admin")
                .OldAnnotation("Npgsql:Enum:attendance_method.attendance_method", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:attendance_status.attendance_status", "PRESENT,ABSENT,LATE,EXCUSED")
                .OldAnnotation("Npgsql:Enum:auth.aal_level", "aal1,aal2,aal3")
                .OldAnnotation("Npgsql:Enum:auth.code_challenge_method", "s256,plain")
                .OldAnnotation("Npgsql:Enum:auth.factor_status", "unverified,verified")
                .OldAnnotation("Npgsql:Enum:auth.factor_type", "totp,webauthn,phone")
                .OldAnnotation("Npgsql:Enum:auth.oauth_authorization_status", "pending,approved,denied,expired")
                .OldAnnotation("Npgsql:Enum:auth.oauth_client_type", "public,confidential")
                .OldAnnotation("Npgsql:Enum:auth.oauth_registration_type", "dynamic,manual")
                .OldAnnotation("Npgsql:Enum:auth.oauth_response_type", "code")
                .OldAnnotation("Npgsql:Enum:auth.one_time_token_type", "confirmation_token,reauthentication_token,recovery_token,email_change_token_new,email_change_token_current,phone_change_token")
                .OldAnnotation("Npgsql:Enum:billing_model_enum.billing_model", "MONTHLY,YEARLY")
                .OldAnnotation("Npgsql:Enum:biometric_req_status.biometric_req_status", "PENDING,APPROVED,REJECTED")
                .OldAnnotation("Npgsql:Enum:enrollment_status.enrollment_status", "ACTIVE,DROPPED")
                .OldAnnotation("Npgsql:Enum:exam_slot_status.exam_slot_status", "SCHEDULED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:institution_status.institution_status", "ACTIVE,INACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:notification_channel.notification_channel", "PUSH,EMAIL,DASHBOARD")
                .OldAnnotation("Npgsql:Enum:notification_type.notification_type", "LOW_BALANCE_ALERT,ATTENDANCE_SESSION_STARTED,EXAM_REMINDER,VIOLATION_DETECTED,BIOMETRIC_REQUEST_STATUS,SERVICE_SUSPENDED")
                .OldAnnotation("Npgsql:Enum:participation_status.participation_status", "JOINED,SUBMITTED,DISQUALIFIED,ABSENT,LEFT")
.OldAnnotation("Npgsql:Enum:pricing_service_type.pricing_service_type", "attendance_unit,proctoring_per_hour,subscription_monthly,subscription_yearly")
                .OldAnnotation("Npgsql:Enum:realtime.action", "INSERT,UPDATE,DELETE,TRUNCATE,ERROR")
                .OldAnnotation("Npgsql:Enum:realtime.equality_op", "eq,neq,lt,lte,gt,gte,in")
                .OldAnnotation("Npgsql:Enum:reference_type_enum.reference_type_enum", "INSTITUTION,ATTENDANCE_SESSION,EXAM_SLOT,TRANSACTION")
                .OldAnnotation("Npgsql:Enum:session_status.session_status", "IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:storage.buckettype", "STANDARD,ANALYTICS,VECTOR")
                .OldAnnotation("Npgsql:Enum:student_exam_record_status.student_exam_record_status", "MARKED,COMPLETED,DELETED")
                .OldAnnotation("Npgsql:Enum:transaction_status.transaction_status", "PENDING,SUCCESS,FAILED,pending,success,failed")
.OldAnnotation("Npgsql:Enum:transaction_type.transaction_type", "TOP_UP,ATTENDANCE_FEE,PROCTORING_FEE,top_up,attendance_fee,proctoring_fee,SUBSCRIPTION_FEE").OldAnnotation("Npgsql:Enum:user_status.user_status", "ACTIVE,BLOCKED")
                .OldAnnotation("Npgsql:Enum:violation_severity.violation_severity", "WARNING,SEVERE")
                .OldAnnotation("Npgsql:Enum:violation_type.violation_type", "IMPERSONATION,GAZE_DIVERSION,MULTIPLE_FACES,ABSENCE,HEAD_TURN,FACE_OBSTRUCTED,TabSwitch,WindowBlur,ExitFullscreen")
                .OldAnnotation("Npgsql:PostgresExtension:extensions.pg_stat_statements", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:extensions.pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:extensions.uuid-ossp", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vault.supabase_vault", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.Sql("ALTER TYPE pricing_service_type ADD VALUE IF NOT EXISTS 'subscription_monthly';");
            migrationBuilder.Sql("ALTER TYPE pricing_service_type ADD VALUE IF NOT EXISTS 'subscription_yearly';");
            migrationBuilder.Sql("ALTER TYPE transaction_type ADD VALUE IF NOT EXISTS 'SUBSCRIPTION_FEE';");

            

           

            migrationBuilder.CreateIndex(
                name: "ux_biometric_active",
                table: "biometric_data",
                column: "user_id",
                filter: "(is_active = true)");

           

         
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
   

            



            migrationBuilder.DropIndex(
                name: "ux_biometric_active",
                table: "biometric_data");

            

            

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
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:app_role.app_role", "STUDENT,LECTURER,SCHOOL_ADMIN,SUPER_ADMIN,student,lecturer,school_admin,super_admin")
                .OldAnnotation("Npgsql:Enum:attendance_method.attendance_method", "AI,MANUAL")
                .OldAnnotation("Npgsql:Enum:attendance_status.attendance_status", "PRESENT,ABSENT,LATE,EXCUSED")
                .OldAnnotation("Npgsql:Enum:auth.aal_level", "aal1,aal2,aal3")
                .OldAnnotation("Npgsql:Enum:auth.code_challenge_method", "s256,plain")
                .OldAnnotation("Npgsql:Enum:auth.factor_status", "unverified,verified")
                .OldAnnotation("Npgsql:Enum:auth.factor_type", "totp,webauthn,phone")
                .OldAnnotation("Npgsql:Enum:auth.oauth_authorization_status", "pending,approved,denied,expired")
                .OldAnnotation("Npgsql:Enum:auth.oauth_client_type", "public,confidential")
                .OldAnnotation("Npgsql:Enum:auth.oauth_registration_type", "dynamic,manual")
                .OldAnnotation("Npgsql:Enum:auth.oauth_response_type", "code")
                .OldAnnotation("Npgsql:Enum:auth.one_time_token_type", "confirmation_token,reauthentication_token,recovery_token,email_change_token_new,email_change_token_current,phone_change_token")
                .OldAnnotation("Npgsql:Enum:billing_model_enum.billing_model", "MONTHLY,YEARLY")
                .OldAnnotation("Npgsql:Enum:biometric_req_status.biometric_req_status", "PENDING,APPROVED,REJECTED")
                .OldAnnotation("Npgsql:Enum:enrollment_status.enrollment_status", "ACTIVE,DROPPED")
                .OldAnnotation("Npgsql:Enum:exam_slot_status.exam_slot_status", "SCHEDULED,IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:institution_status.institution_status", "ACTIVE,INACTIVE,SUSPENDED")
                .OldAnnotation("Npgsql:Enum:notification_channel.notification_channel", "PUSH,EMAIL,DASHBOARD")
                .OldAnnotation("Npgsql:Enum:notification_type.notification_type", "LOW_BALANCE_ALERT,ATTENDANCE_SESSION_STARTED,EXAM_REMINDER,VIOLATION_DETECTED,BIOMETRIC_REQUEST_STATUS,SERVICE_SUSPENDED")
                .OldAnnotation("Npgsql:Enum:participation_status.participation_status", "JOINED,SUBMITTED,DISQUALIFIED,ABSENT,LEFT")
                .OldAnnotation("Npgsql:Enum:pricing_service_type.pricing_service_type", "attendance_unit,proctoring_per_hour,subscription_monthly,subscription_yearly")
                .OldAnnotation("Npgsql:Enum:realtime.action", "INSERT,UPDATE,DELETE,TRUNCATE,ERROR")
                .OldAnnotation("Npgsql:Enum:realtime.equality_op", "eq,neq,lt,lte,gt,gte,in")
                .OldAnnotation("Npgsql:Enum:reference_type_enum.reference_type_enum", "INSTITUTION,ATTENDANCE_SESSION,EXAM_SLOT,TRANSACTION")
                .OldAnnotation("Npgsql:Enum:session_status.session_status", "IN_PROGRESS,COMPLETED,CANCELLED")
                .OldAnnotation("Npgsql:Enum:storage.buckettype", "STANDARD,ANALYTICS,VECTOR")
                .OldAnnotation("Npgsql:Enum:student_exam_record_status.student_exam_record_status", "MARKED,COMPLETED,DELETED")
                .OldAnnotation("Npgsql:Enum:transaction_status.transaction_status", "PENDING,SUCCESS,FAILED,pending,success,failed")
                .OldAnnotation("Npgsql:Enum:transaction_type.transaction_type", "TOP_UP,ATTENDANCE_FEE,PROCTORING_FEE,top_up,attendance_fee,proctoring_fee,SUBSCRIPTION_FEE")
                .OldAnnotation("Npgsql:Enum:user_status.user_status", "ACTIVE,BLOCKED")
                .OldAnnotation("Npgsql:Enum:violation_severity.violation_severity", "WARNING,SEVERE")
                .OldAnnotation("Npgsql:Enum:violation_type.violation_type", "IMPERSONATION,GAZE_DIVERSION,MULTIPLE_FACES,ABSENCE,HEAD_TURN,FACE_OBSTRUCTED,TabSwitch,WindowBlur,ExitFullscreen")
                .OldAnnotation("Npgsql:PostgresExtension:extensions.pg_stat_statements", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:extensions.pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:extensions.uuid-ossp", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vault.supabase_vault", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateIndex(
                name: "ux_biometric_active",
                table: "biometric_data",
                column: "user_id",
                unique: true,
                filter: "(is_active = true)");

            
        }
    }
}
