using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Security.Claims;
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
        public async Task<ActionResult<List<Account>>> GetAccounts()
        {
            try
            {
                var accounts = await _context.Accounts
                    .Select(a => new
                    {
                        a.Id,
                        a.Name,
                        a.Email,
                        a.Role,
                        a.isActive,
                        a.Specialization,
                        a.CreatedAt,
                        a.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return BadRequest($"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetAllUsersWithStats()
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
                        a.Specialization,
                        a.CreatedAt,
                        ProjectCount = _context.ProjectMembers
                            .Count(pm => pm.AccountId == a.Id),
                        ActiveTaskCount = _context.TaskAssignments
                            .Count(ta => ta.AccountId == a.Id &&
                                        !ta.IsDeleted &&
                                        _context.Tasks.Any(t => t.Id == ta.TaskId &&
                                                               !t.IsDeleted &&
                                                               (t.StatusId == 1 || t.StatusId == 2 || t.StatusId == 3)))
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
        public async Task<IActionResult> GetAccountById(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
                return NotFound("Account not found.");

            return Ok(new
            {
                account.Name,
                account.Email,
                account.Role,
                account.Specialization,
                account.isActive,
                account.ProfilePicture,
            });
        }

        [HttpPost]
        public async Task<ActionResult<Account>> CreateAccount([FromBody] CreateAccountDTO dto, [FromQuery] int adminId)
        {
            var admin = await _context.Accounts.FindAsync(adminId);
            if (admin == null || admin.Role != "Admin")
                return StatusCode(403, "Access denied. Admins only.");

            if (dto is null)
                return BadRequest();

            if (!System.Text.RegularExpressions.Regex.IsMatch(
                dto.Email,
                @"^[^@\s]+@[^@\s]+\.com$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return BadRequest("Email must follow the format example@domain.com.");
            }

            if (dto.Password.Length < 8)
                return BadRequest("Password must be at least 8 characters.");
            if (!dto.Password.Any(char.IsUpper))
                return BadRequest("Password must contain at least one uppercase letter.");
            if (!dto.Password.Any(char.IsLower))
                return BadRequest("Password must contain at least one lowercase letter.");
            if (!dto.Password.Any(char.IsDigit))
                return BadRequest("Password must contain at least one number.");
            if (!dto.Password.Any(ch => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(ch)))
                return BadRequest("Password must contain at least one special character (!@#$%^&*...).");

            var emailExists = await _context.Accounts
                .AnyAsync(a => a.Email.ToLower() == dto.Email.ToLower());
            if (emailExists)
                return BadRequest("Email already exists.");

            var newAccount = new Account
            {
                Name = dto.Name,
                Email = dto.Email,
                Specialization = dto.Specialization,
                Role = dto.Role,
                isActive = dto.isActive,
                CreatedAt = PhTime,
                UpdatedAt = PhTime
            };

            newAccount.PasswordHash = _passwordHasher.HashPassword(newAccount, dto.Password);

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
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAccountById), new { id = newAccount.Id }, new
            {
                newAccount.Id,
                newAccount.Name,
                newAccount.Email,
                newAccount.Role,
                newAccount.Specialization,
                newAccount.isActive,
                newAccount.CreatedAt
            });
        }
        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromQuery] int editorId, [FromBody] UpdateAccountDto updatedAccount)
        {
            // Fetch the account being edited
            var existingAccount = await _context.Accounts.FindAsync(id);
            if (existingAccount == null)
                return NotFound("Account not found.");

            // Fetch the editor (who is making the change)
            var editorAccount = await _context.Accounts.FindAsync(editorId);
            if (editorAccount == null)
                return NotFound("Editor account not found.");

            var changes = new List<string>();

            if (updatedAccount.Name != null && updatedAccount.Name != existingAccount.Name)
            {
                changes.Add("Name");
                existingAccount.Name = updatedAccount.Name;
            }

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

                changes.Add("Password");
            }

            if (updatedAccount.Role != null && updatedAccount.Role != existingAccount.Role)
            {
                changes.Add("Role");
                existingAccount.Role = updatedAccount.Role;
            }

            if (updatedAccount.isActive.HasValue && updatedAccount.isActive.Value != existingAccount.isActive)
            {
                changes.Add("Active Status");
                existingAccount.isActive = updatedAccount.isActive.Value;
            }

            if (updatedAccount.ProfilePicture != null && updatedAccount.ProfilePicture != existingAccount.ProfilePicture)
            {
                changes.Add("Profile Picture");
                existingAccount.ProfilePicture = updatedAccount.ProfilePicture;
            }

            if (updatedAccount.Specialization != null && updatedAccount.Specialization != existingAccount.Specialization)
            {
                changes.Add("Specialization");
                existingAccount.Specialization = updatedAccount.Specialization;
            }

            if (!changes.Any())
                return Ok(new { message = "No changes detected." });

            existingAccount.UpdatedAt = PhTime;

            var fieldSummary = changes.Count == 1
                ? changes[0]
                : string.Join(", ", changes[..^1]) + " and " + changes[^1];

            bool isSelfEdit = editorId == id;
            string note = isSelfEdit
                ? $"{existingAccount.Name} updated their own {fieldSummary}."
                : $"The {fieldSummary} of {existingAccount.Name}'s account was updated by Admin {editorAccount.Name}.";

            _context.AuditLogs.Add(new AuditLog
            {
                AccountId = id,
                Action = "Updated",
                NewValue = string.Join(", ", changes),
                Note = note,
                CreatedAt = PhTime
            });

            await _context.SaveChangesAsync();

            string responseMessage = isSelfEdit
                ? $"Your {fieldSummary} {(changes.Count == 1 ? "has" : "have")} been updated successfully."
                : $"The {fieldSummary} of {existingAccount.Name}'s account {(changes.Count == 1 ? "has" : "have")} been updated successfully.";

            return Ok(new
            {
                message = responseMessage,
                updatedFields = changes,
                account = new
                {
                    existingAccount.Name,
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