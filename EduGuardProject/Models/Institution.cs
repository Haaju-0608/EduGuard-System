using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

[Table("institutions")]
public partial class Institution
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string SubDomain { get; set; } = null!;

    public string ContactEmail { get; set; } = null!;

    public DateTime? SubscriptionExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    [Column("billing_model")]
    public BillingModel BillingModel { get; set; }

    [Column("status")]
    public InstitutionStatus Status { get; set; }

    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual Wallet? Wallet { get; set; }
}
