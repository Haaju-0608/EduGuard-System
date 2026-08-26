namespace EduGuardProject.Models;

public partial class ReadingPassage
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    public string ExamQuestionName { get; set; } = null!;

    public string PassageText { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Institution Institution { get; set; } = null!;

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}
