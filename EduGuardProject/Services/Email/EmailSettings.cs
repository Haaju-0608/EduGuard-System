namespace EduGuardProject.Services.Email;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";

    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string AppPassword { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;
}
