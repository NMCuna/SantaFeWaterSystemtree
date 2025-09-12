using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class EmailSettings
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "SMTP Server")]
        public string SmtpServer { get; set; } = string.Empty;

        [Required]
        [Display(Name = "SMTP Port")]
        public int SmtpPort { get; set; }

        [Required]
        [Display(Name = "Sender Name")]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Sender Email")]
        public string SenderEmail { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Sender Password")]
        public string SenderPassword { get; set; } = string.Empty;
    }
}
