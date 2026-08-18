namespace EduGuardProject.Models;

public partial class ExamQuestion
{
    public Guid Id { get; set; }

    public Guid ExamSlotId { get; set; }

    public Guid? PassageId { get; set; }

    public string QuestionType { get; set; } = null!;

    public string QuestionContent { get; set; } = null!;

    public string? AudioUrl { get; set; }

    public string? ImageUrl { get; set; }

    public decimal Points { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ExamSlot ExamSlot { get; set; } = null!;

    public virtual ReadingPassage? Passage { get; set; }

    public virtual ICollection<QuestionOption> QuestionOptions { get; set; } = new List<QuestionOption>();
}

