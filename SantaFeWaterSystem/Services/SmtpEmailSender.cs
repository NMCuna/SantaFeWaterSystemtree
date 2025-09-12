using System.Net;
using System.Net.Mail;
using SantaFeWaterSystem.Data;
using Microsoft.EntityFrameworkCore;

public class SmtpEmailSender : IEmailSender
{
    private readonly ApplicationDbContext _context;

    public SmtpEmailSender(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        var settings = await _context.EmailSettings.FirstOrDefaultAsync();
        if (settings == null)
            throw new Exception("Email settings not configured.");

        var mail = new MailMessage
        {
            From = new MailAddress(settings.SenderEmail, settings.SenderName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        mail.To.Add(toEmail);

        using var smtp = new SmtpClient(settings.SmtpServer, settings.SmtpPort)
        {
            Credentials = new NetworkCredential(settings.SenderEmail, settings.SenderPassword),
            EnableSsl = true
        };

        await smtp.SendMailAsync(mail);
    }
}
