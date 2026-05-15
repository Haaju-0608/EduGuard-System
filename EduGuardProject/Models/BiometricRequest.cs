using System;
using System.Collections.Generic;

namespace EduGuardProject.Models;

public partial class BiometricRequest
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid? ApprovedBy { get; set; }

    public string Reason { get; set; } = null!;

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual ICollection<BiometricDatum> BiometricData { get; set; } = new List<BiometricDatum>();

    public virtual User Student { get; set; } = null!;
}
