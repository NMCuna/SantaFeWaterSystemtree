// Models/HomePageContent.cs
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class HomePageContent
    {
        public int Id { get; set; }

        // Make title and subtitle nullable
        public string? Title { get; set; }
        public string? Subtitle { get; set; }

        // Card 1 - Nullable
        public string? Card1Title { get; set; }
        public string? Card1Text { get; set; }
        public string? Card1Icon { get; set; } = "bi-droplet-half";

        // Card 2 - Nullable
        public string? Card2Title { get; set; }
        public string? Card2Text { get; set; }
        public string? Card2Icon { get; set; } = "bi-credit-card-2-front";

        // Card 3 - Nullable
        public string? Card3Title { get; set; }
        public string? Card3Text { get; set; }
        public string? Card3Icon { get; set; } = "bi-person-lines-fill";

        // Card 4 - Nullable
        public string? Card4Title { get; set; }
        public string? Card4Text { get; set; }
        public string? Card4Icon { get; set; } = "bi-gear-fill";

        // Card 5 - Nullable
        public string? Card5Title { get; set; }
        public string? Card5Text { get; set; }
        public string? Card5Icon { get; set; } = "bi-info-circle-fill";
    }
}
