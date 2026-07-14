using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Request
{
    public class CreatePricingConfigDto
    {
        public PricingServiceType ServiceType { get; set; }
        [System.ComponentModel.DataAnnotations.Range(
            typeof(decimal),
            "0.01",
            "79228162514264337593543950335")]
        public decimal UnitPrice { get; set; }
        public DateTime EffectiveDate { get; set; }
    }
}
