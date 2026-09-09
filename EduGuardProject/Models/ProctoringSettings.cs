using System;
using System.Collections.Generic;

namespace EduGuardProject.Models;

/// <summary>
/// Configurable AI proctoring behavior. InstitutionId == null is the system-wide default,
/// used as a fallback when an institution has no override.
/// </summary>
public partial class ProctoringSettings
{
    public Guid Id { get; set; }

    public Guid? InstitutionId { get; set; }

    /// <summary>
    /// Max AI-detected violations allowed for a student (browser violations are NOT counted here).
    /// </summary>
    public int MaxAiViolationCount { get; set; }

    /// <summary>
    /// Cooldown window: after an AI violation is recorded, no further AI violation is recorded
    /// for this many seconds (browser violations are NOT subject to this cooldown).
    /// </summary>
    public int CooldownSeconds { get; set; }

    /// <summary>
    /// If false, two consecutive AI violations of the same type back-to-back are collapsed into one.
    /// </summary>
    public bool AllowConsecutiveSameType { get; set; }

    /// <summary>
    /// AI violation count at which the lecturer is notified to decide whether to disqualify.
    /// Reaching this threshold does NOT auto-disqualify the student.
    /// </summary>
    public int AiNotifyThreshold { get; set; }

    /// <summary>
    /// Browser violation count at which the lecturer is notified to decide whether to disqualify.
    /// Reaching this threshold does NOT auto-disqualify the student.
    /// </summary>
    public int BrowserNotifyThreshold { get; set; }

    public bool IsActive { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Institution? Institution { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual User? UpdatedByNavigation { get; set; }

    public virtual ICollection<ProctoringViolationTypeSetting> ViolationTypeSettings { get; set; } = new List<ProctoringViolationTypeSetting>();
}
