namespace EduGuardProject.Models;

public partial class QuestionOption
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }

    public string OptionLabel { get; set; } = null!;

    public string OptionContent { get; set; } = null!;

    public bool IsCorrect { get; set; }

    public virtual ExamQuestion Question { get; set; } = null!;
}

