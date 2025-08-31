using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class Permission
    {
        public int Id { get; set; }
        public string Name { get; set; }  // e.g. "ManageUsers", "ManageConsumers"
        public string Description { get; set; }
    }

}
