namespace EduGuardProject.Hubs;

public static class HubEvents
{
    public const string NotificationHubConnected = nameof(NotificationHubConnected);
    public const string InstitutionNotificationsJoined = nameof(InstitutionNotificationsJoined);
    public const string SuperAdminNotificationsJoined = nameof(SuperAdminNotificationsJoined);
    public const string NotificationCreated = nameof(NotificationCreated);
    public const string NewSchoolRegistrationRequest = nameof(NewSchoolRegistrationRequest);
    public const string WalletBalanceUpdated = nameof(WalletBalanceUpdated);
    public const string NotificationRead = nameof(NotificationRead);
    public const string NotificationsRead = nameof(NotificationsRead);

    public const string DashboardHubConnected = nameof(DashboardHubConnected);
    public const string DashboardScopeJoined = nameof(DashboardScopeJoined);
    public const string ResourceChanged = nameof(ResourceChanged);
    public const string DashboardStatsChanged = nameof(DashboardStatsChanged);
    public const string ReportDataChanged = nameof(ReportDataChanged);
    public const string StorageObjectChanged = nameof(StorageObjectChanged);

    public const string ExamJoined = nameof(ExamJoined);
    public const string ExamLeft = nameof(ExamLeft);
    public const string ExamDashboardJoined = nameof(ExamDashboardJoined);
    public const string StudentOnline = nameof(StudentOnline);
    public const string StudentOffline = nameof(StudentOffline);
    public const string StudentHeartbeat = nameof(StudentHeartbeat);
    public const string StudentJoinedExam = nameof(StudentJoinedExam);
    public const string ExamSubmitted = nameof(ExamSubmitted);
    public const string ViolationDetected = nameof(ViolationDetected);
    public const string Disqualified = nameof(Disqualified);

    public const string AttendanceSessionJoined = nameof(AttendanceSessionJoined);
    public const string AttendanceSessionLeft = nameof(AttendanceSessionLeft);
    public const string ClassAttendanceJoined = nameof(ClassAttendanceJoined);
    public const string AttendanceSessionOpened = nameof(AttendanceSessionOpened);
    public const string AttendanceProgressChanged = nameof(AttendanceProgressChanged);
    public const string AttendanceCompleted = nameof(AttendanceCompleted);
}
