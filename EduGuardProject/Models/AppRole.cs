namespace EduGuardProject.Models;
using NpgsqlTypes;


//ENUM cho User
public enum AppRole
{
    [PgName("STUDENT")] // Khi đọc từ DB chữ 'STUDENT', nó sẽ map vào giá trị này
    Student,     // Vị trí 0: Mặc định nếu lỡ quên gán quyền

    [PgName("LECTURER")]
    Lecturer,     // Vị trí 1

    [PgName("SCHOOL_ADMIN")]
    SchoolAdmin, // Vị trí 2

    [PgName("SUPER_ADMIN")]
    SuperAdmin
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

//ENUM cho Transaction:
public enum TransactionType
{
    TOP_UP,          // Nạp tiền vào ví
    ATTENDANCE_FEE,  // Phí điểm danh
    PROCTORING_FEE   // Phí giám thị / quét phòng thi
}

public enum TransactionStatus
{
    PENDING,
    SUCCESS,
    FAILED
}

//ENUM cho Pricing
public enum PricingServiceType
{
    ATTENDANCE_UNIT,      // Tính phí theo lượt điểm danh
    PROCTORING_PER_HOUR   // Tính phí giám thị theo giờ
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