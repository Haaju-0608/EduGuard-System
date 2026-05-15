using System;
using System.Collections.Generic;

namespace EduGuardProject.Models;

public partial class ViolationLog
{
    public Guid Id { get; set; }

    public Guid ParticipationId { get; set; }

    /// <summary>
    /// Bucket: exam-evidence
    /// </summary>
    public string? EvidencePath { get; set; }

    public double? AiConfidence { get; set; }

    public bool IsReviewed { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual ExamParticipation Participation { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}
