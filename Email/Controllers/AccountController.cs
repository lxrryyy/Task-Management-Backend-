using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Threading.Tasks;
using TaskManagement.Data;
using TaskManagement.DTOs.Account;
using TaskManagement.Models;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly AccountDbContext _context;
        private readonly PasswordHasher<Account> _passwordHasher = new PasswordHasher<Account>();
        private readonly IConfiguration _config;
		private static DateTime PhTime =>
        	TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
		public AccountController(AccountDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpGet]
        public async Task<ActionResult<List<Account>>> GetAccountsv1()
        {
            try
            {
                return Ok(await _context.Accounts.ToListAsync());
            }
            catch (Exception ex)
            {
                return BadRequest($"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<ActionResult<Account>> GetAllUserRoleAccount()
        {
            try
            {
                var users = await _context.Accounts
                    .Where(a => a.Role == "User" && a.isActive)
                    .Select(a => new
                    {
                        a.Id,
                        a.Name,
                        a.Email,
                        a.Role,
                        a.ProfilePicture
                    })
                    .ToListAsync();

                if (!users.Any())
                    return NotFound("No users found.");

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }

        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Account>> GetAccountById(int id, [FromQuery] int adminId)
        {
            var admin = await _context.Accounts.FindAsync(adminId);
            if (admin == null || admin.Role != "Admin")
                return StatusCode(403, "Access denied. Admins only.");

            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
                return NotFound();

            return Ok(account);
        }

        [HttpPost]
        public async Task<ActionResult<Account>> CreateAccount([FromBody] Account newAccount, [FromQuery] int adminId)
        {
            var admin = await _context.Accounts.FindAsync(adminId);
            if (admin == null || admin.Role != "Admin")
                return StatusCode(403, "Access denied. Admins only.");

            if (newAccount is null)
                return BadRequest();

            if (!System.Text.RegularExpressions.Regex.IsMatch(
                newAccount.Email,
                @"^[^@\s]+@[^@\s]+\.com$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return BadRequest("Email must follow the format example@domain.com.");
            }
            var password = newAccount.PasswordHash;
            if (password.Length < 8)
                return BadRequest("Password must be at least 8 characters.");
            if (!password.Any(char.IsUpper))
                return BadRequest("Password must contain at least one uppercase letter.");
            if (!password.Any(char.IsLower))
                return BadRequest("Password must contain at least one lowercase letter.");
            if (!password.Any(char.IsDigit))
                return BadRequest("Password must contain at least one number.");
            if (!password.Any(ch => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(ch)))
                return BadRequest("Password must contain at least one special character (!@#$%^&*...).");

            var emailExists = await _context.Accounts
                .AnyAsync(a => a.Email.ToLower() == newAccount.Email.ToLower());
            if (emailExists)
                return BadRequest("Email already exists.");

            newAccount.PasswordHash = _passwordHasher.HashPassword(newAccount, newAccount.PasswordHash);
            newAccount.CreatedAt = PhTime;
            newAccount.UpdatedAt = PhTime;

            _context.Accounts.Add(newAccount);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                AccountId = adminId,
                Action = "CREATED",
                NewValue = newAccount.Name,
                Note = $"User {newAccount.Name} was created by {admin.Name}.",
                CreatedAt = PhTime
            });
            return CreatedAtAction(nameof(GetAccountById), new { id = newAccount.Id }, newAccount);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountDto updatedAccount)
        {
            var existingAccount = await _context.Accounts.FindAsync(id);
            if (existingAccount == null)
                return NotFound();

            var changes = new List<string>();
            if (updatedAccount.CurrentPassword != null ||
                updatedAccount.NewPassword != null ||
                updatedAccount.ConfirmPassword != null)
            {
                if (string.IsNullOrWhiteSpace(updatedAccount.CurrentPassword))
                    return BadRequest("Current password is required.");
                if (string.IsNullOrWhiteSpace(updatedAccount.NewPassword))
                    return BadRequest("New password is required.");
                if (string.IsNullOrWhiteSpace(updatedAccount.ConfirmPassword))
                    return BadRequest("Confirm password is required.");

                var verifyResult = _passwordHasher.VerifyHashedPassword(
                    existingAccount,
                    existingAccount.PasswordHash,
                    updatedAccount.CurrentPassword);

                if (verifyResult == PasswordVerificationResult.Failed)
                    return BadRequest("Current password is incorrect.");

                if (updatedAccount.NewPassword != updatedAccount.ConfirmPassword)
                    return BadRequest("New password and confirm password do not match.");

                if (updatedAccount.CurrentPassword == updatedAccount.NewPassword)
                    return BadRequest("New password must be different from the current password.");

                existingAccount.PasswordHash = _passwordHasher.HashPassword(
                    existingAccount,
                    updatedAccount.NewPassword);

                changes.Add("Password updated");
            }
            if (updatedAccount.Role != null && updatedAccount.Role != existingAccount.Role)
            {
                changes.Add($"Role: {existingAccount.Role} → {updatedAccount.Role}");
                existingAccount.Role = updatedAccount.Role;
            }
            if (updatedAccount.isActive.HasValue && updatedAccount.isActive.Value != existingAccount.isActive)
            {
                changes.Add($"isActive: {existingAccount.isActive} → {updatedAccount.isActive.Value}");
                existingAccount.isActive = updatedAccount.isActive.Value;
            }
            if (updatedAccount.ProfilePicture != null && updatedAccount.ProfilePicture != existingAccount.ProfilePicture)
            {
                changes.Add("ProfilePicture updated");
                existingAccount.ProfilePicture = updatedAccount.ProfilePicture;
            }
            if (updatedAccount.Specialization != null && updatedAccount.Specialization != existingAccount.Specialization)
            {
                changes.Add($"Specialization: {existingAccount.Specialization ?? "None"} → {updatedAccount.Specialization}");
                existingAccount.Specialization = updatedAccount.Specialization;
            }

            if (!changes.Any())
                return Ok(new { message = "No changes detected." });

            existingAccount.UpdatedAt = PhTime;
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                AccountId = id,
                Action = "Updated",
                NewValue = string.Join(", ", changes),
                Note = $"Account updated by {existingAccount.Name} ({existingAccount.Role})"
            });
            await _context.SaveChangesAsync();

            var updatedFieldNames = changes
                .Select(c => c.Split(':')[0].Trim())  
                .ToList();

            var fieldSummary = updatedFieldNames.Count == 1
                ? updatedFieldNames[0]
                : string.Join(", ", updatedFieldNames[..^1]) + " and " + updatedFieldNames[^1];

            return Ok(new
            {
                message = $"{fieldSummary} {(updatedFieldNames.Count == 1 ? "has" : "have")} been updated successfully.",
                updatedFields = changes,
                account = new
                {
                    existingAccount.Role,
                    existingAccount.Specialization,
                    existingAccount.isActive,
                    existingAccount.ProfilePicture,
                    existingAccount.UpdatedAt
                }
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveProfilePicture(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            var existingAccount = _context.Accounts.Find(account);
            if (existingAccount == null)
                return NotFound();
            existingAccount.ProfilePicture = null;
            _context.AuditLogs.Add(new AuditLog
            {
                AccountId = account.Id,
                Action = "DELETED",
                Note = $"Profile picture removed by {account.Name}, {account.Role}.",
                CreatedAt = PhTime
            });
            await _context.SaveChangesAsync();
            return Ok("Profile picture has been removed");
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(int id, [FromQuery] int adminId)
        {
            var admin = await _context.Accounts.FindAsync(adminId);
            if (admin == null || admin.Role != "Admin")
                return StatusCode(403, "Access denied. Admins only.");

            var existingAccount = _context.Accounts.Find(id);
            if (existingAccount == null)
                return NotFound();

            existingAccount.isActive = false;

            _context.AuditLogs.Add(new AuditLog
            {
                AccountId = adminId,
                Action = "DELETED",
                Note = $"User {existingAccount.Name} was deactivated by {admin.Name}.",
                CreatedAt = PhTime
            });

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> ReactivateAccount(int id, [FromQuery] int adminId)
        {
            var admin = await _context.Accounts.FindAsync(adminId);
            if (admin == null || admin.Role != "Admin")
                return StatusCode(403, "Access denied. Admins only.");

            var existingAccount = _context.Accounts.Find(id);
            if (existingAccount == null)
                return NotFound();

            existingAccount.isActive = true;

            _context.AuditLogs.Add(new AuditLog
            {
                AccountId = adminId,
                Action = "RESTORED",
                Note = $"Account Reactivated by {admin.Name}.",
                CreatedAt = PhTime
            });

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("UploadProfilePicture/{id}")]
        public async Task<IActionResult> UploadProfilePicture(int id, IFormFile file)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
                return NotFound();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Only image files are allowed.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{id}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            account.ProfilePicture = $"/uploads/profiles/{fileName}";
            account.UpdatedAt = PhTime;
            await _context.SaveChangesAsync();

            return Ok(new { profilePicture = account.ProfilePicture });
        }
    }
}