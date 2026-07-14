using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class PricingConfig
{
    public Guid Id { get; set; }

    [Column("service_type")]
    public PricingServiceType ServiceType { get; set; }

    /// <summary>
    /// Pricing in VND
    /// </summary>
    public decimal UnitPrice { get; set; }

    public DateTime EffectiveDate { get; set; }

    public bool IsActive { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual User? UpdatedByNavigation { get; set; }
}
