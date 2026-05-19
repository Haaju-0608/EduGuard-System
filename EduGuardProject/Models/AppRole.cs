namespace EduGuardProject.Models;
using NpgsqlTypes;

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
