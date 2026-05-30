using System.ComponentModel.DataAnnotations;
using Pgvector;

namespace EduGuardProject.Models;

public partial class BiometricDatum
{
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public Guid? BioRequestId { get; set; }

    public Vector? FaceVector { get; set; }

    /// <summary>
    /// AI model version used to generate embedding
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ModelVersion { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Bucket: biometric-faces
    /// </summary>
    [MaxLength(500)]
    public string? FaceImageUrl { get; set; }

    public virtual BiometricRequest? BioRequest { get; set; }

    public virtual User User { get; set; } = null!;
}
