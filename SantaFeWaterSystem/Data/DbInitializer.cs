using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.Data
{
    public static class DbInitializer
    {
        public static void Seed(ApplicationDbContext context)
        {
            if (!context.EmailSettings.Any())
            {
                context.EmailSettings.Add(new EmailSettings
                {
                    SmtpServer = "smtp.gmail.com",
                    SmtpPort = 587,
                    SenderName = "Santa Fe Water System",
                    SenderEmail = "your-email@gmail.com",
                    SenderPassword = "app-password"
                });
                context.SaveChanges();
            }
        }
    }
}
