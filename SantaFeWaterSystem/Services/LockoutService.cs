using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace SantaFeWaterSystem.Services
{
    public class LockoutService
    {
        private readonly ApplicationDbContext _context;

        public LockoutService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(int MaxFailed, int DurationMinutes)> GetPolicyAsync()
        {
            var policy = await _context.LockoutPolicies.FirstOrDefaultAsync();
            return (policy?.MaxFailedAccessAttempts ?? 5, policy?.LockoutMinutes ?? 15);
        }

        public async Task<(bool IsLocked, string Message)> ApplyFailedAttemptAsync(User user)
        {
            var (maxFailed, duration) = await GetPolicyAsync();

            user.AccessFailedCount++;

            if (user.AccessFailedCount >= maxFailed)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(duration);
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return (true, $"Account locked due to too many failed attempts. Try again after {duration} minutes.");
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return (false, "Invalid username or password.");
        }

        public async Task ResetLockoutAsync(User user)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public bool IsCurrentlyLocked(User user)
        {
            return user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow;
        }
    }
}
