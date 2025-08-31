using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.ViewModels
{
    public class ManageUsersViewModel
    {
        public List<User> Admins { get; set; } = new();
        public List<User> Staffs { get; set; } = new();
        public List<User> Users { get; set; } = new();

        public string SearchTerm { get; set; } = "";
        public string SelectedRole { get; set; } = "";
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int AdminCount { get; set; }
        public int StaffCount { get; set; }
        public int UserCount { get; set; }
        public string AdminSearchTerm { get; set; }
        public string StaffSearchTerm { get; set; }
        public string UserSearchTerm { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalStaffs { get; set; }
        public int TotalUsers { get; set; }



    }
}
