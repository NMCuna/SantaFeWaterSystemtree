using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using SantaFeWaterSystem.Data;

public class PermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<List<string>> GetUserPermissionsAsync(int userId)
    {
        var staff = await _context.Users.FirstOrDefaultAsync(s => s.Id == userId);

        if (staff == null)
            return new List<string>();

        if (staff.Role == "Admin")
        {
            return await _context.Permissions
                .Select(p => p.Name)
                .ToListAsync();
        }

        return await _context.StaffPermissions
            .Where(sp => sp.StaffId == userId)
            .Select(sp => sp.Permission.Name)
            .ToListAsync();
    }
}
