using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data; // Adjust namespace to your project
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class RateController(ApplicationDbContext context, PermissionService permissionService, AuditLogService audit)
     : BaseController(permissionService, context, audit)
    {




        //================== INDEX RATE VIEW LIST ==================

        // GET: Rate
        public async Task<IActionResult> Index()
        {
            var rates = await _context.Rates.ToListAsync();
            return View(rates);
        }




        //================== CREATE RATE ==================

        // GET: Rate/Create
        public IActionResult Create()
        {
            PopulateAccountTypesDropdown();

            var latestRate = _context.Rates
                .OrderByDescending(r => r.EffectiveDate)
                .FirstOrDefault();

            var model = new Rate
            {
                AccountType = latestRate?.AccountType ?? ConsumerType.Residential,
                RatePerCubicMeter = latestRate?.RatePerCubicMeter ?? 13m,
                PenaltyAmount = latestRate?.PenaltyAmount ?? 10m,
                EffectiveDate = DateTime.Today
            };

            return View(model);
        }

        // POST: Rate/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Rate rate)
        {
            PopulateAccountTypesDropdown();

            if (ModelState.IsValid)
            {
                // Check if rate with same AccountType and EffectiveDate already exists
                bool exists = _context.Rates.Any(r =>
                    r.AccountType == rate.AccountType &&
                    r.EffectiveDate.Date == rate.EffectiveDate.Date);

                if (exists)
                {
                    ModelState.AddModelError(nameof(rate.EffectiveDate), "A rate for this account type and effective date already exists.");
                    return View(rate);
                }

                // Save the rate
                _context.Add(rate);
                await _context.SaveChangesAsync();

                // Add audit trail entry
                var audit = new AuditTrail
                {
                    Action = "Create Rate",
                    PerformedBy = User.Identity?.Name ?? "Unknown",
                    Timestamp = DateTime.Now,
                    Details = $"Created new rate:\n" +
                              $"- Account Type: {rate.AccountType}\n" +
                              $"- Rate Per Cubic Meter: {rate.RatePerCubicMeter:C}\n" +
                              $"- Penalty Amount: {rate.PenaltyAmount:C}\n" +
                              $"- Effective Date: {rate.EffectiveDate:yyyy-MM-dd}"
                };

                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Rate successfully created.";
                return RedirectToAction(nameof(Index));
            }

            return View(rate);
        }





        //================== EDIT RATE ==================

        // GET: Rate/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var rate = await _context.Rates.FindAsync(id);
            if (rate == null) return NotFound();

            PopulateAccountTypesDropdown();
            return View(rate);
        }

        // POST: Rate/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Rate rate)
        {
            if (id != rate.Id) return NotFound();

            PopulateAccountTypesDropdown();

            if (ModelState.IsValid)
            {
                // Check for duplicate effective date for the same AccountType excluding current record
                bool exists = _context.Rates.Any(r =>
                    r.AccountType == rate.AccountType &&
                    r.EffectiveDate.Date == rate.EffectiveDate.Date &&
                    r.Id != rate.Id);

                if (exists)
                {
                    ModelState.AddModelError(nameof(rate.EffectiveDate), "A rate for this account type and effective date already exists.");
                    return View(rate);
                }

                try
                {
                    var oldRate = await _context.Rates.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rate.Id);

                    _context.Update(rate);
                    await _context.SaveChangesAsync();

                    // Log to AuditTrail
                    var audit = new AuditTrail
                    {
                        Action = "Edit Rate",
                        PerformedBy = User.Identity?.Name ?? "Unknown",
                        Timestamp = DateTime.Now,
                        Details =
                            $"Edited Rate ID: {rate.Id}\n" +
                            $"Old Account Type: {oldRate?.AccountType}, New: {rate.AccountType}\n" +
                            $"Old Rate: {oldRate?.RatePerCubicMeter:C}, New: {rate.RatePerCubicMeter:C}\n" +
                            $"Old Penalty: {oldRate?.PenaltyAmount:C}, New: {rate.PenaltyAmount:C}\n" +
                            $"Old Effective Date: {oldRate?.EffectiveDate:yyyy-MM-dd}, New: {rate.EffectiveDate:yyyy-MM-dd}"
                    };

                    _context.AuditTrails.Add(audit);
                    await _context.SaveChangesAsync();

                    TempData["Message"] = "Rate successfully updated.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RateExists(rate.Id))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(rate);
        }




        //================== DELETE RATE ==================

        // GET: Rate/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var rate = await _context.Rates.FirstOrDefaultAsync(m => m.Id == id);
            if (rate == null) return NotFound();

            return View(rate);
        }

        // POST: Rate/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rate = await _context.Rates.FindAsync(id);
            if (rate != null)
            {
                // Store details before deleting
                string details = $"Deleted Rate:\n" +
                                 $"- Account Type: {rate.AccountType}\n" +
                                 $"- Rate per Cubic Meter: {rate.RatePerCubicMeter:C}\n" +
                                 $"- Penalty Amount: {rate.PenaltyAmount:C}\n" +
                                 $"- Effective Date: {rate.EffectiveDate:yyyy-MM-dd}";

                _context.Rates.Remove(rate);
                await _context.SaveChangesAsync();

                // Log to AuditTrail
                var audit = new AuditTrail
                {
                    Action = "Delete Rate",
                    PerformedBy = User.Identity?.Name ?? "Unknown",
                    Timestamp = DateTime.Now,
                    Details = details
                };

                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();

                TempData["Message"] = "Rate deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }



        //================== HELPER METHODS ==================
        private bool RateExists(int id)
        {
            return _context.Rates.Any(e => e.Id == id);
        }



        //================== DROPDOWN POPULATE ==================

        // Populate Account Types Dropdown
        private void PopulateAccountTypesDropdown()
        {
            var accountTypes = Enum.GetValues(typeof(ConsumerType))
                .Cast<ConsumerType>()
                .Select(at => new SelectListItem
                {
                    Value = at.ToString(),
                    Text = at.ToString()
                })
                .ToList();

            ViewBag.AccountTypes = accountTypes;
        }
    }
}
