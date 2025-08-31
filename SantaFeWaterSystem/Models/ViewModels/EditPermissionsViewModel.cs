namespace SantaFeWaterSystem.ViewModels
{
    public class PermissionCheckbox
    {
        public int PermissionId { get; set; }
        public string Name { get; set; }
        public bool IsAssigned { get; set; }
    }

    public class EditPermissionsViewModel
    {
        public int StaffId { get; set; }
        public List<PermissionCheckbox> Permissions { get; set; }
    }
}
