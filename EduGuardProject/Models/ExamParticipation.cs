using System;
using System.Collections.Generic;

namespace EduGuardProject.Models;

public partial class ExamParticipation
{
    public Guid Id { get; set; }

    public Guid ExamSlotId { get; set; }

    public Guid StudentId { get; set; }

    public Guid? BillingTransId { get; set; }

    public DateTime? ActualStart { get; set; }

    public DateTime? ActualEnd { get; set; }

    public string? DisqualifiedReason { get; set; }

    /// <summary>
    /// Bucket: exam-recordings
    /// </summary>
    public string? RecordingVideoPath { get; set; }

    /// <summary>
    /// Bucket: exam-identity
    /// </summary>
    public string? IdentitySnapshotPath { get; set; }

    public virtual Transaction? BillingTrans { get; set; }

    public virtual ExamSlot ExamSlot { get; set; } = null!;

    public virtual User Student { get; set; } = null!;

    public virtual ICollection<ViolationLog> ViolationLogs { get; set; } = new List<ViolationLog>();
}
