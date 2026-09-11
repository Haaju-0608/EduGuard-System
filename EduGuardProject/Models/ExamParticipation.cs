using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace EduGuardProject.Models;

public partial class ExamParticipation
{
    public Guid Id { get; set; }

    public Guid ExamSlotId { get; set; }

    public Guid StudentId { get; set; }

    public DateTime? IdentityVerifiedAt { get; set; }
    public Guid? IdentityVerifiedBy { get; set; }

    public Guid? BillingTransId { get; set; }

    public DateTime? ActualStart { get; set; }

    public DateTime? ActualEnd { get; set; }

    [Column("status")]
    public ParticipationStatus Status { get; set; }

    public string? DisqualifiedReason { get; set; }

    /// <summary>
    /// Bucket: exam-recordings
    /// </summary>
    public string? RecordingVideoPath { get; set; }

    /// <summary>
    /// Bucket: exam-identity
    /// </summary>
    public string? IdentitySnapshotPath { get; set; }

    // Snapshot of ProctoringSettings taken once at CreateAsync (exam start), so the rules in
    // force for a participation stay fixed for its whole exam even if a SchoolAdmin changes the
    // institution's settings mid-exam. Null only for participations created before this feature
    // existed — callers must fall back to ProctoringSettingsService.GetEffectiveAsync in that case.
    public int? MaxAiViolationCountSnapshot { get; set; }
    public int? CooldownSecondsSnapshot { get; set; }
    public bool? AllowConsecutiveSameTypeSnapshot { get; set; }
    public int? AiNotifyThresholdSnapshot { get; set; }
    public int? BrowserNotifyThresholdSnapshot { get; set; }

    public virtual Transaction? BillingTrans { get; set; }

    public virtual ExamSlot ExamSlot { get; set; } = null!;

    public virtual User Student { get; set; } = null!;

    public virtual ICollection<ViolationLog> ViolationLogs { get; set; } = new List<ViolationLog>();

    public virtual User? IdentityVerifiedByNavigation { get; set; }

}
