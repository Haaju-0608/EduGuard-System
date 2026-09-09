using System;
using System.Text.Json.Serialization;

namespace EduGuardProject.Models;

/// <summary>
/// Per-violation-type detection timing for a ProctoringSettings row
/// (e.g. how many seconds of continuous gaze diversion before it counts as one violation).
/// </summary>
public partial class ProctoringViolationTypeSetting
{
    public Guid Id { get; set; }

    public Guid ProctoringSettingsId { get; set; }

    public ViolationType ViolationType { get; set; }

    public int DetectionThresholdSeconds { get; set; }

    [JsonIgnore]
    public virtual ProctoringSettings ProctoringSettings { get; set; } = null!;
}
