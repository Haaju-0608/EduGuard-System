using EduGuardProject.Models;
using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    //hứng dữ liệu khi tạo mới
    public class CreateInstitutionDto
    {
        [Required(ErrorMessage = "Institution name is required.")]
        [MaxLength(255, ErrorMessage = "Name must not exceed 255 characters.")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Sub-domain is required.")]
        [MaxLength(100)]
        [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Sub-domain can only contain lowercase letters, numbers, and hyphens.")]
        public string SubDomain { get; set; } = null!;

        [Required(ErrorMessage = "Contact email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(255)]
        public string ContactEmail { get; set; } = null!;

        [Required(ErrorMessage = "Billing model is required.")]
        public BillingModel BillingModel { get; set; }

        public InstitutionStatus Status { get; set; } = InstitutionStatus.Active;
    }

    public class UpdateInstitutionDto
    {
        [Required(ErrorMessage = "Institution name is required.")]
        [MaxLength(255)]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Sub-domain is required.")]
        [MaxLength(100)]
        [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Sub-domain can only contain lowercase letters, numbers, and hyphens.")]
        public string SubDomain { get; set; } = null!;

        [Required(ErrorMessage = "Contact email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(255)]
        public string ContactEmail { get; set; } = null!;

        [Required(ErrorMessage = "Billing model is required.")]
        public BillingModel BillingModel { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        public InstitutionStatus Status { get; set; }
    }

}
