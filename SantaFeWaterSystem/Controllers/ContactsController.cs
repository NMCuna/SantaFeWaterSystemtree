using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers.Admin
{
    [Authorize(Roles = "Admin,Staff")]
    public class ContactsController : Controller
    {
        private readonly ApplicationDbContext _context;
        protected readonly AuditLogService _audit;

        public ContactsController(ApplicationDbContext context, AuditLogService audit)
        {
            _context = context;
            _audit = audit;
        }


        //================== Contact Management ==================
        // GET: Admin/Contacts
        public async Task<IActionResult> Index()
        {
            var contacts = await _context.ContactInfos.ToListAsync();
            return View(contacts);
        }



        //================== CREATE CONTACT ==================

        // GET: Admin/Contacts/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Contacts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContactInfo model)
        {
            if (ModelState.IsValid)
            {
                _context.ContactInfos.Add(model);
                await _context.SaveChangesAsync();

                // Audit trail
                var performedBy = User.Identity?.Name ?? "Unknown";
                var details = $"New contact created. Name={model.FacebookName}, Email={model.Email}, Phone={model.Phone}.";

                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

                var audit = new AuditTrail
                {
                    Action = "Create Contact",
                    PerformedBy = performedBy,
                    Timestamp = DateTime.Now,
                    Details = details
                };

                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Contact created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }




        //================== EDIT CONTACT ==================

        // GET: Admin/Contacts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var contact = await _context.ContactInfos.FindAsync(id);
            if (contact == null)
                return NotFound();

            return View(contact);
        }

        // POST: Admin/Contacts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ContactInfo model)
        {
            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Get original contact for audit details
                    var originalContact = await _context.ContactInfos.AsNoTracking()
                                                .FirstOrDefaultAsync(c => c.Id == id);

                    _context.Update(model);
                    await _context.SaveChangesAsync();

                    // Audit trail
                    var performedBy = User.Identity?.Name ?? "Unknown";
                    var details = $"Contact updated. " +
                                  $"Before: Name={originalContact.FacebookName}, Email={originalContact.Email}, Phone={originalContact.Phone}. " +
                                  $"After: Name={model.FacebookName}, Email={model.Email}, Phone={model.Phone}.";

                    var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                    var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

                    var audit = new AuditTrail
                    {
                        Action = "Edit Contact",
                        PerformedBy = performedBy,
                        Timestamp = DateTime.Now,
                        Details = details
                    };
                    _context.AuditTrails.Add(audit);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Contact updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ContactExists(model.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }



        //================== DELETE CONTACT ==================

        // GET: Admin/Contacts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var contact = await _context.ContactInfos.FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null)
                return NotFound();

            return View(contact);
        }

        // POST: Admin/Contacts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contact = await _context.ContactInfos.FindAsync(id);
            if (contact != null)
            {
                _context.ContactInfos.Remove(contact);
                await _context.SaveChangesAsync();

                // Audit trail
                var performedBy = User.Identity?.Name ?? "Unknown";
                var details = $"Contact deleted. Name={contact.FacebookName}, Email={contact.Email}, Phone={contact.Phone}.";
                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
                var audit = new AuditTrail
                {
                    Action = "Delete Contact",
                    PerformedBy = performedBy,
                    Timestamp = DateTime.Now,
                    Details = details
                };
                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Contact deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }




        //================== HELPER METHODS ==================

        // Helper method to check if contact exists
        private bool ContactExists(int id)
        {
            return _context.ContactInfos.Any(e => e.Id == id);
        }
    }
}
