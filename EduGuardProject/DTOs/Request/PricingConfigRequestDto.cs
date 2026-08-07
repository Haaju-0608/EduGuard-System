using EduGuardProject.Models;
using System.ComponentModel.DataAnnotations;

namespace EduGuardProject.DTOs.Request
{
    public class CreatePricingConfigDto
    {
        [Required(ErrorMessage = "Service type is required.")]
        public PricingServiceType ServiceType { get; set; }

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
            ErrorMessage = "Unit price must be greater than 0.")]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "Effective date is required.")]
        public DateTime EffectiveDate { get; set; }
    }

    public class UpdatePricingConfigDto
    {
        [Required(ErrorMessage = "Service type is required.")]
        public PricingServiceType ServiceType { get; set; }

        [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
            ErrorMessage = "Unit price must be greater than 0.")]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "Effective date is required.")]
        public DateTime EffectiveDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
