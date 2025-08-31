using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BackupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public BackupController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: Backup Dashboard
        public IActionResult Index()
        {
            return View();
        }

        // Backup database
        public async Task<IActionResult> BackupDatabase()
        {
            string backupFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Backups");
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            string backupFile = Path.Combine(backupFolder, $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string sql = $"BACKUP DATABASE [{connection.Database}] TO DISK='{backupFile}' WITH FORMAT, MEDIANAME='DBBackup', NAME='Full Backup of {connection.Database}'";
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        await command.ExecuteNonQueryAsync();
                        connection.Close();
                    }
                }

                // Log backup
                _context.BackupLogs.Add(new BackupLog
                {
                    Action = "Backup",
                    FileName = Path.GetFileName(backupFile),
                    PerformedBy = User.Identity?.Name,
                    ActionDate = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Backup created successfully: {backupFile}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Backup failed: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // Restore database - GET
        [HttpGet]
        public IActionResult RestoreDatabase()
        {
            return View();
        }

        // Restore database - POST
        [HttpPost]
        public async Task<IActionResult> RestoreDatabase(IFormFile backupFile)
        {
            if (backupFile == null || backupFile.Length == 0)
            {
                TempData["Error"] = "Please select a backup file.";
                return RedirectToAction("RestoreDatabase");
            }

            string backupFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Backups");
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            string filePath = Path.Combine(backupFolder, backupFile.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await backupFile.CopyToAsync(stream);
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string databaseName = connection.Database;

                    string setSingleUser = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                    string restoreSql = $"RESTORE DATABASE [{databaseName}] FROM DISK='{filePath}' WITH REPLACE";
                    string setMultiUser = $"ALTER DATABASE [{databaseName}] SET MULTI_USER";

                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand(setSingleUser, connection))
                        await cmd.ExecuteNonQueryAsync();
                    using (SqlCommand cmd = new SqlCommand(restoreSql, connection))
                        await cmd.ExecuteNonQueryAsync();
                    using (SqlCommand cmd = new SqlCommand(setMultiUser, connection))
                        await cmd.ExecuteNonQueryAsync();
                    connection.Close();
                }

                // Log restore
                _context.BackupLogs.Add(new BackupLog
                {
                    Action = "Restore",
                    FileName = backupFile.FileName,
                    PerformedBy = User.Identity?.Name,
                    ActionDate = DateTime.Now
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "Database restored successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Restore failed: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // List existing backups
        public IActionResult ManageBackups()
        {
            string backupFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Backups");
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            var backups = Directory.GetFiles(backupFolder)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            return View(backups);
        }

        // Download backup
        public IActionResult DownloadBackup(string fileName)
        {
            string backupFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Backups");
            string filePath = Path.Combine(backupFolder, fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            return PhysicalFile(filePath, "application/octet-stream", fileName);
        }

        // Delete backup
        public IActionResult DeleteBackup(string fileName)
        {
            string backupFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Backups");
            string filePath = Path.Combine(backupFolder, fileName);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            TempData["Success"] = $"Backup {fileName} deleted.";
            return RedirectToAction("ManageBackups");
        }

        // Backup history logs
        public IActionResult BackupHistory()
        {
            var logs = _context.BackupLogs.OrderByDescending(b => b.ActionDate).ToList();
            return View(logs);
        }













        [NonAction] // Prevent direct HTTP access
        public async Task ScheduledBackup()
        {
            string backupFolder = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Backups");
            if (!Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            string backupFile = Path.Combine(backupFolder, $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string sql = $"BACKUP DATABASE [{connection.Database}] TO DISK='{backupFile}' WITH FORMAT, MEDIANAME='DBBackup', NAME='Scheduled Backup of {connection.Database}'";
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        await command.ExecuteNonQueryAsync();
                        connection.Close();
                    }
                }

                // Log scheduled backup
                _context.BackupLogs.Add(new BackupLog
                {
                    Action = "Scheduled Backup",
                    FileName = Path.GetFileName(backupFile),
                    PerformedBy = "System",
                    ActionDate = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Optional: log error
            }
        }

    }
}
