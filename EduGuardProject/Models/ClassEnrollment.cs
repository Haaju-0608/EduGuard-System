using System;
using System.Collections.Generic;

namespace EduGuardProject.Models;

public partial class ClassEnrollment
{
    public Guid ClassId { get; set; }

    public Guid StudentId { get; set; }

    public DateTime EnrolledAt { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
