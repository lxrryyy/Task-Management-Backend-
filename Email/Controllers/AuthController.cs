using TaskManagement.Data;
using TaskManagement.DTOs;
using TaskManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AccountDbContext _context;

        public AuthController(AccountDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Invalid request");

                var normalizedEmail = request.Email.Trim().ToLowerInvariant();
                var account = await _context.Accounts
                    .SingleOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail);

                if (account == null)
                    return Unauthorized("Invalid credentials");

                var hasher = new PasswordHasher<Account>();
                var verification = hasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);

                if (verification == PasswordVerificationResult.Failed)
                    return Unauthorized("Invalid credentials");

                // Generate a random token
                var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

                var apiToken = new ApiToken
                {
                    Token = token,
                    AccountId = account.Id,
                    Revoked = false,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(8)
                };

                _context.ApiTokens.Add(apiToken);
                await _context.SaveChangesAsync();

                return Ok(new 
                    { 
                        token, 
                        expiresIn = 28800,
                        user = new
                        {
                            account.Id,
                            account.Name,
                            account.Email,
                            account.Role,
                            account.ProfilePicture
                        }
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            try
            {
                // Try header first, then body
                var authHeader = Request.Headers["Authorization"].ToString();

                string token;
                if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? authHeader.Substring(7).Trim()
                        : authHeader.Trim();
                }
                else if (request != null && !string.IsNullOrWhiteSpace(request.Token))
                {
                    token = request.Token.Trim();
                }
                else
                {
                    return BadRequest("No token provided.");
                }

                var apiToken = await _context.ApiTokens
                    .SingleOrDefaultAsync(t => t.Token == token && !t.Revoked);

                if (apiToken == null)
                    return Unauthorized("Invalid or already revoked token.");

                apiToken.Revoked = true;
                await _context.SaveChangesAsync();

                return Ok("Logged out successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        [HttpGet("me/{id}")]
        public async Task<IActionResult> Me(int id)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            Console.WriteLine($"=== AUTH HEADER RECEIVED: '{authHeader}' ==="); // 👈 add this

            if (string.IsNullOrWhiteSpace(authHeader))
                return Unauthorized("Missing token.");

            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring(7).Trim()
                : authHeader.Trim();

            var apiToken = await _context.ApiTokens
                .SingleOrDefaultAsync(t => t.Token == token && !t.Revoked && t.ExpiresAt > DateTime.UtcNow);

            if (apiToken == null)
                return Unauthorized("Invalid or expired token.");

            var account = await _context.Accounts.FindAsync(id);

            if (account == null)
                return NotFound("Account not found.");

            return Ok(new
            {
                account.Id,
                account.Name,
                account.Email,
                account.Role,
                account.CreatedAt,
                account.UpdatedAt,
                account.isActive,
                apiToken.ExpiresAt
            });
        }
    }
}
