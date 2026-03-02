using TaskManagement.Data;
using TaskManagement.DTOs;
using TaskManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AccountController(AccountDbContext context) : ControllerBase
    {
        private readonly AccountDbContext _context = context;
        private readonly PasswordHasher<Account> _passwordHasher = new PasswordHasher<Account>();

        [HttpGet]
        public async Task<ActionResult<List<Account>>> GetAccountsv1([FromQuery] int adminId)
        {
            var admin = await _context.Accounts.FindAsync(adminId);
            if (admin == null || admin.Role != "Admin")
                return StatusCode(403, "Access denied. Admins only.");

            try
            {
                return Ok(await _context.Accounts.ToListAsync());
            }
            catch (Exception ex)
            {
                return BadRequest($"Internal server error: {ex.Message}");
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

            newAccount.PasswordHash = _passwordHasher.HashPassword(newAccount, newAccount.PasswordHash);
            newAccount.CreatedAt = DateTime.UtcNow;
            newAccount.UpdatedAt = DateTime.UtcNow;

            _context.Accounts.Add(newAccount);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAccountById), new { id = newAccount.Id }, newAccount);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateAccount(int id, [FromBody] UpdateAccountDto updatedAccount, [FromQuery] int adminId)
        {
            var admin = await _context.Accounts.FindAsync(adminId);
            if (admin == null || admin.Role != "Admin")
                return StatusCode(403, "Access denied. Admins only.");

            var existingAccount = await _context.Accounts.FindAsync(id);
            if (existingAccount == null)
                return NotFound();

            if (updatedAccount.Name != null)
                existingAccount.Name = updatedAccount.Name;
            if (updatedAccount.Email != null)
                existingAccount.Email = updatedAccount.Email;
            if (updatedAccount.PasswordHash != null)
                existingAccount.PasswordHash = _passwordHasher.HashPassword(existingAccount, updatedAccount.PasswordHash);
            if (updatedAccount.Role != null)
                existingAccount.Role = updatedAccount.Role;
            if (updatedAccount.isActive.HasValue)
                existingAccount.isActive = updatedAccount.isActive.Value;
            if (updatedAccount.ProfilePicture != null)
                existingAccount.ProfilePicture = updatedAccount.ProfilePicture;

            existingAccount.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
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

            _context.Accounts.Remove(existingAccount);
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
            account.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { profilePicture = account.ProfilePicture });
        }
    }
}