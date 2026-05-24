using EduGuardProject.Models;

namespace EduGuardProject.DTOs.Response
{
    public class PricingConfigResponseDto
    {
        public Guid Id { get; set; }
        public PricingServiceType ServiceType { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime EffectiveDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
