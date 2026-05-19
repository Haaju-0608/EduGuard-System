using System;
using System.Collections.Generic;
using Pgvector;

namespace EduGuardProject.Models;

public partial class BiometricDatum
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? BioRequestId { get; set; }

    public Vector? FaceVector { get; set; } 

    /// <summary>
    /// AI model version used to generate embedding
    /// </summary>
    public string ModelVersion { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Bucket: biometric-faces
    /// </summary>
    public string? FaceImageUrl { get; set; }

    public virtual BiometricRequest? BioRequest { get; set; }

    public virtual User User { get; set; } = null!;
}
