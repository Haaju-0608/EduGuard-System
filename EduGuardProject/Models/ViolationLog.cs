using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class ViolationLog
{
    public Guid Id { get; set; }

    public Guid ParticipationId { get; set; }

    /// <summary>
    /// Bucket: exam-evidence
    /// </summary>
    public string? EvidencePath { get; set; }
    [Column("severity")]
    public ViolationSeverity severity { get; set; }
    [Column("violation_type")]
    public ViolationType violationType { get; set; }
    public double? AiConfidence { get; set; }

    public bool IsReviewed { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual ExamParticipation Participation { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}
