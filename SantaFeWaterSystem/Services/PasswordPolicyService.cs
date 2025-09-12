using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Services
{
    public class PasswordPolicyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public PasswordPolicyService(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // Get current password policy
        public async Task<PasswordPolicy> GetPolicyAsync()
        {
            return await _context.PasswordPolicies.FirstOrDefaultAsync() ?? new PasswordPolicy();
        }

        // Validate a password against policy & history
        public async Task<bool> ValidatePasswordAsync(int userId, string password)
        {
            var policy = await GetPolicyAsync();

            // Length check
            if (password.Length < policy.MinPasswordLength)
                return false;

            // Complexity check
            if (policy.RequireComplexity)
            {
                bool hasUpper = Regex.IsMatch(password, "[A-Z]");
                bool hasLower = Regex.IsMatch(password, "[a-z]");
                bool hasDigit = Regex.IsMatch(password, "[0-9]");
                bool hasSymbol = Regex.IsMatch(password, "[^a-zA-Z0-9]");
                if (!(hasUpper && hasLower && hasDigit && hasSymbol))
                    return false;
            }

            // History check
            var recentPasswords = await _context.PasswordHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.ChangedDate)
                .Take(policy.PasswordHistoryCount)
                .ToListAsync();

            foreach (var oldPassword in recentPasswords)
            {
                var dummyUser = new User();
                var result = _passwordHasher.VerifyHashedPassword(dummyUser, oldPassword.PasswordHash, password);
                if (result == PasswordVerificationResult.Success)
                    return false;
            }

            return true;
        }

        // Save password to history (with cleanup)
        public async Task SavePasswordHistoryAsync(int userId, string passwordHash)
        {
            // 1️⃣ Save new password
            _context.PasswordHistories.Add(new PasswordHistory
            {
                UserId = userId,
                PasswordHash = passwordHash,
                ChangedDate = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // 2️⃣ Cleanup: keep only last N entries
            var policy = await GetPolicyAsync();
            var historyLimit = policy.PasswordHistoryCount > 0 ? policy.PasswordHistoryCount : 5; // default 5

            var oldPasswords = await _context.PasswordHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.ChangedDate)
                .Skip(historyLimit) // skip newest N
                .ToListAsync();

            if (oldPasswords.Any())
            {
                _context.PasswordHistories.RemoveRange(oldPasswords);
                await _context.SaveChangesAsync();
            }
        }
}
}