using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models
{
    [Table("contact_requests")] // Ép C# gọi đúng tên bảng chữ thường dưới DB
    public class ContactRequest
    {
        [Key]
        [Column("id")] // Ép C# gọi đúng tên cột chữ thường
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("school_name")]
        public string SchoolName { get; set; } = string.Empty;

        [Column("contact_person_name")]
        public string ContactPersonName { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("message")]
        public string? Message { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("status")]
        public string Status { get; set; } = "PENDING"; // PENDING, CONTACTED, APPROVED
    }
}
