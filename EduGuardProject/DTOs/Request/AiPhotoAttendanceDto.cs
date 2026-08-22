using Microsoft.AspNetCore.Http;

namespace EduGuardProject.DTOs.Request
{
    public class AiPhotoAttendanceDto
    {
        public IFormFile PhotoFile { get; set; } = null!;
    }
}