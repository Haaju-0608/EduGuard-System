using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    public class ContactRequestDto
    {
        [Required(ErrorMessage = "School name is required.")]
        [StringLength(150, ErrorMessage = "School name cannot exceed 150 characters.")]
        public string SchoolName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact person name is required.")]
        public string ContactPersonName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Message cannot exceed 500 characters.")]
        public string? Message { get; set; }
    }
}