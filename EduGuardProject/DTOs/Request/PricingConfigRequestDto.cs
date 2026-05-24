using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Request
{
    public class CreatePricingConfigDto
    {
        public PricingServiceType ServiceType { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime EffectiveDate { get; set; }
    }
}
