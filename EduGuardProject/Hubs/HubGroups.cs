using EduGuardProject.Models;

namespace EduGuardProject.Hubs;

public static class HubGroups
{
    public static string User(Guid userId) => $"user:{userId}";

    public static string Role(AppRole role) => role switch
    {
        AppRole.Student => "role:STUDENT",
        AppRole.Lecturer => "role:LECTURER",
        AppRole.SchoolAdmin => "role:SCHOOL_ADMIN",
        AppRole.SuperAdmin => "role:SUPER_ADMIN",
        _ => $"role:{role}"
    };

    public static string Institution(Guid institutionId) => $"institution:{institutionId}";

    public static string InstitutionAdmins(Guid institutionId) => $"institution:{institutionId}:admins";

    public static string Exam(Guid examSlotId) => $"exam:{examSlotId}";

    public static string ExamLecturers(Guid examSlotId) => $"exam:{examSlotId}:lecturers";

    public static string ExamStudent(Guid examSlotId, Guid studentId) => $"exam:{examSlotId}:student:{studentId}";

    public static string Attendance(Guid sessionId) => $"attendance:{sessionId}";

    public static string AttendanceLecturers(Guid sessionId) => $"attendance:{sessionId}:lecturers";

    public static string ClassStudents(Guid classId) => $"class:{classId}:students";
}
