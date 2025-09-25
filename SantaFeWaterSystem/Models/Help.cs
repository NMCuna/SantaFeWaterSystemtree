using System;

namespace SantaFeWaterSystem.Models
{
    public class Help
    {
        public int Id { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }

        
        public string? Section { get; set; }

       
        public string? RoleAccess { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
