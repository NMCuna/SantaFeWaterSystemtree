using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class StaffPermission
    {
        public int Id { get; set; }
        public int StaffId { get; set; }          // FK to User.Id of staff user
        public User Staff { get; set; }
        public int PermissionId { get; set; }     // FK to Permission.Id
        public Permission Permission { get; set; }
    }
}

