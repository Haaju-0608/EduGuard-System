namespace EduGuardProject.Models;

using NpgsqlTypes;


//ENUM cho User
public enum AppRole
{
    [PgName("STUDENT")]
    Student,

    [PgName("LECTURER")]
    Lecturer,

    [PgName("SCHOOL_ADMIN")]
    SchoolAdmin,

    [PgName("SUPER_ADMIN")]
    SuperAdmin,

    [PgName("student")]
    Student_LEGACY,

    [PgName("lecturer")]
    Lecturer_LEGACY,

    [PgName("school_admin")]
    SchoolAdmin_LEGACY,

    [PgName("super_admin")]
    SuperAdmin_LEGACY
}

public static class AppRoleExtensions
{
    public static AppRole ToCanonical(this AppRole role) =>
        role switch
        {
            AppRole.Student_LEGACY => AppRole.Student,
            AppRole.Lecturer_LEGACY => AppRole.Lecturer,
            AppRole.SchoolAdmin_LEGACY => AppRole.SchoolAdmin,
            AppRole.SuperAdmin_LEGACY => AppRole.SuperAdmin,
            _ => role
        };

    public static bool IsInRoles(this AppRole role, params AppRole[] allowedRoles) =>
        allowedRoles.Any(allowed => role.ToCanonical() == allowed.ToCanonical());
}

public enum UserStatus
{
    [PgName("ACTIVE")]
    Active,

    [PgName("BLOCKED")]
    Blocked
}


//ENUM cho Institution
public enum BillingModel
{
    [PgName("PAY_AS_YOU_GO")]
    PayAsYouGo,

    [PgName("SUBSCRIPTION")]
    Subscription
}

public enum InstitutionStatus
{
    [PgName("ACTIVE")]
    Active,

    [PgName("INACTIVE")]
    Inactive,

    [PgName("SUSPENDED")]
    Suspended
}

//ENUM cho Transaction
public enum TransactionType
{
    [PgName("TOP_UP")]
    TOP_UP,

    [PgName("ATTENDANCE_FEE")]
    ATTENDANCE_FEE,

    [PgName("PROCTORING_FEE")]
    PROCTORING_FEE,

    [PgName("top_up")]
    TOP_UP_LEGACY,

    [PgName("attendance_fee")]
    ATTENDANCE_FEE_LEGACY,

    [PgName("proctoring_fee")]
    PROCTORING_FEE_LEGACY
}

public static class TransactionTypeExtensions
{
    public static bool IsTopUp(this TransactionType type) =>
        type is TransactionType.TOP_UP or TransactionType.TOP_UP_LEGACY;

    public static bool IsAttendanceFee(this TransactionType type) =>
        type is TransactionType.ATTENDANCE_FEE or TransactionType.ATTENDANCE_FEE_LEGACY;

    public static bool IsProctoringFee(this TransactionType type) =>
        type is TransactionType.PROCTORING_FEE or TransactionType.PROCTORING_FEE_LEGACY;

    public static string ToCanonicalName(this TransactionType type) =>
        type switch
        {
            TransactionType.TOP_UP or TransactionType.TOP_UP_LEGACY => nameof(TransactionType.TOP_UP),
            TransactionType.ATTENDANCE_FEE or TransactionType.ATTENDANCE_FEE_LEGACY => nameof(TransactionType.ATTENDANCE_FEE),
            TransactionType.PROCTORING_FEE or TransactionType.PROCTORING_FEE_LEGACY => nameof(TransactionType.PROCTORING_FEE),
            _ => type.ToString()
        };
}

public enum TransactionStatus
{
    [PgName("PENDING")]
    PENDING,

    [PgName("SUCCESS")]
    SUCCESS,

    [PgName("FAILED")]
    FAILED,

    [PgName("pending")]
    PENDING_LEGACY,

    [PgName("success")]
    SUCCESS_LEGACY,

    [PgName("failed")]
    FAILED_LEGACY
}

public static class TransactionStatusExtensions
{
    public static bool IsPending(this TransactionStatus status) =>
        status is TransactionStatus.PENDING or TransactionStatus.PENDING_LEGACY;

    public static bool IsSuccess(this TransactionStatus status) =>
        status is TransactionStatus.SUCCESS or TransactionStatus.SUCCESS_LEGACY;

    public static string ToCanonicalName(this TransactionStatus status) =>
        status switch
        {
            TransactionStatus.PENDING or TransactionStatus.PENDING_LEGACY => nameof(TransactionStatus.PENDING),
            TransactionStatus.SUCCESS or TransactionStatus.SUCCESS_LEGACY => nameof(TransactionStatus.SUCCESS),
            TransactionStatus.FAILED or TransactionStatus.FAILED_LEGACY => nameof(TransactionStatus.FAILED),
            _ => status.ToString()
        };
}

//ENUM cho Pricing
public enum PricingServiceType
{
    [PgName("attendance_unit")]
    ATTENDANCE_UNIT,

    [PgName("proctoring_per_hour")]
    PROCTORING_PER_HOUR
}

//ENUM cho notification
public enum NotificationType
{
    [PgName("LOW_BALANCE_ALERT")]
    LowBalanceAlert,

    [PgName("ATTENDANCE_SESSION_STARTED")]
    AttendanceSessionStarted,

    [PgName("EXAM_REMINDER")]
    ExamReminder,

    [PgName("VIOLATION_DETECTED")]
    ViolationDetected,

    [PgName("BIOMETRIC_REQUEST_STATUS")]
    BiometricRequestStatus,

    [PgName("SERVICE_SUSPENDED")]
    ServiceSuspended
}

public enum NotificationChannel
{
    [PgName("PUSH")]
    Push,

    [PgName("EMAIL")]
    Email,

    [PgName("DASHBOARD")]
    Dashboard
}

public enum ReferenceTypeEnum
{
    [PgName("INSTITUTION")]
    Institution,

    [PgName("ATTENDANCE_SESSION")]
    AttendanceSession,

    [PgName("EXAM_SLOT")]
    ExamSlot,

    [PgName("TRANSACTION")]
    Transaction
}

//ENUM cho attendance session
public enum SessionStatus
{
    [PgName("IN_PROGRESS")]
    InProgress,

    [PgName("COMPLETED")]
    Completed,

    [PgName("CANCELLED")]
    Cancelled
}

//ENUM cho attendance record
public enum AttendanceMethod
{
    [PgName("AI")]
    Ai,

    [PgName("MANUAL")]
    Manual
}

public enum AttendanceStatus
{
    [PgName("PRESENT")]
    Present,

    [PgName("ABSENT")]
    Absent,

    [PgName("LATE")]
    Late,

    [PgName("EXCUSED")]
    Excused
}

//ENUM cho biometric request
public enum BiometricReqStatus
{
    [PgName("PENDING")]
    Pending,

    [PgName("APPROVED")]
    Approved,

    [PgName("REJECTED")]
    Rejected
}

//ENUM cho class enrollment
public enum EnrollmentStatus
{
    [PgName("ACTIVE")]
    Active,

    [PgName("DROPPED")]
    Dropped
}

//ENUM cho exam slot
public enum ExamSlotStatus
{
    [PgName("SCHEDULED")]
    Scheduled,

    [PgName("IN_PROGRESS")]
    InProgress,

    [PgName("COMPLETED")]
    Completed,

    [PgName("CANCELLED")]
    Cancelled
}

//ENUM cho exam-participation
public enum ParticipationStatus
{
    [PgName("JOINED")]
    Joined,

    [PgName("SUBMITTED")]
    Submitted,

    [PgName("DISQUALIFIED")]
    Disqualified,

    [PgName("ABSENT")]
    Absent,

    [PgName("LEFT")]
    Left
}

//ENUM cho student exam record
public enum StudentExamRecordStatus
{
    [PgName("MARKED")]
    Marked = 0,

    [PgName("COMPLETED")]
    Completed = 1,

    [PgName("DELETED")]
    Deleted = 2
}

//ENUM cho violation log
public enum ViolationSeverity
{
    [PgName("WARNING")]
    Warning,

    [PgName("SEVERE")]
    Severe

}

public enum ViolationType
{
    [PgName("IMPERSONATION")]
    Impersonation,

    [PgName("GAZE_DIVERSION")]
    GazeDiversion,

    [PgName("MULTIPLE_FACES")]
    MultipleFaces,

    [PgName("ABSENCE")]
    Absence,

    [PgName("HEAD_TURN")]
    HeadTurn,

    [PgName("FACE_OBSTRUCTED")]
    FaceObstructed,

    //Browser violation types
    [PgName("TAB_SWITCH")]
    TabSwitch,

    [PgName("WINDOW_BLUR")]
    WindowBlur,

    [PgName("EXIT_FULLSCREEN")]
    ExitFullscreen
}
