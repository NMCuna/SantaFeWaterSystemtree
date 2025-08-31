using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class ContactInfo
    {
        [Key]
        public int Id { get; set; }

        [Required, Display(Name = "Phone Number")]
        [MaxLength(50)]
        public string Phone { get; set; }

        [Required, Display(Name = "Email Address")]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; }

        [Required, Display(Name = "Facebook URL")]
        [Url]
        [MaxLength(200)]
        public string FacebookUrl { get; set; }

        [Required, Display(Name = "Facebook Display Name")]
        [MaxLength(100)]
        public string FacebookName { get; set; }

        [Display(Name = "Intro Text")]
        public string IntroText { get; set; }

        [Display(Name = "Water Meter Section Heading")]
        public string WaterMeterHeading { get; set; }

        [Display(Name = "Water Meter Instructions")]
        public string WaterMeterInstructions { get; set; }
    }
}
