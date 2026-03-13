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
using TaskManagement.Data;
using TaskManagement.DTOs.Account;
using TaskManagement.DTOs.Auth;
using TaskManagement.Models;
using TaskManagement.Services;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AccountDbContext _context;
        private readonly IEmailService _emailService;

        public AuthController(AccountDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
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

                var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

                var expiry = request.RememberMe ? DateTime.UtcNow.AddDays(7) : DateTime.UtcNow.AddHours(1);

                account.ApiToken = token;
                account.TokenExpiresAt = expiry;
                account.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    token,
                    expiresIn = request.RememberMe ? 604800 : 28800, 
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
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromHeader(Name = "Authorization")] string? authHeader)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                    return BadRequest("No token provided.");

                var token = authHeader.Substring("Bearer ".Length).Trim();

                var account = await _context.Accounts
                    .SingleOrDefaultAsync(a => a.ApiToken == token);

                account.ApiToken = null;
                account.TokenExpiresAt = null;
                account.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok("Logged out successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me([FromHeader(Name = "Authorization")] string? authHeader)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                    return Unauthorized("No token provided.");

                var token = authHeader.Substring("Bearer ".Length).Trim();

                var account = await _context.Accounts
                    .SingleOrDefaultAsync(a => a.ApiToken == token);

                if (account == null)
                    return Unauthorized("Invalid token.");

                if (account.TokenExpiresAt == null || account.TokenExpiresAt < DateTime.UtcNow)
                {
                    account.ApiToken = null;
                    account.TokenExpiresAt = null;
                    await _context.SaveChangesAsync();
                    return Unauthorized("Token has expired. Please log in again.");
                }

                return Ok(new
                {
                    account.Id,
                    account.Name,
                    account.Email,
                    account.Role,
                    account.ProfilePicture
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                    return BadRequest("Email is required.");

                var normalizedEmail = request.Email.Trim().ToLowerInvariant();
                var account = await _context.Accounts
                    .SingleOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail);

                if (account == null)
                    return NotFound("No account found with that email.");

                var existingOtps = await _context.OtpCodes
                    .Where(o => o.AccountId == account.Id && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();
                foreach (var old in existingOtps)
                    old.IsUsed = true;

                var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

                _context.OtpCodes.Add(new OtpCode
                {
                    AccountId = account.Id,
                    Code = otp,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                await _emailService.SendOtpAsync(normalizedEmail, account.Name, otp);
                return Ok("OTP sent to your email. Valid for 15 minutes.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("VerifyOtp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                var normalizedEmail = request.Email.Trim().ToLowerInvariant();

                var account = await _context.Accounts
                    .SingleOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail);

                if (account == null)
                    return NotFound("No account found with that email.");

                var otpRecord = await _context.OtpCodes
                    .Where(o => o.AccountId == account.Id && o.Code == request.Code && !o.IsUsed)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                if (otpRecord == null)
                    return BadRequest("Invalid OTP.");

                if (otpRecord.ExpiresAt < DateTime.UtcNow)
                    return BadRequest("OTP has expired.");

                otpRecord.IsUsed = true;
                await _context.SaveChangesAsync();

                return Ok("OTP verified. You may now reset your password.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var normalizedEmail = request.Email.Trim().ToLowerInvariant();
                var account = await _context.Accounts
                    .SingleOrDefaultAsync(a => a.Email.ToLower() == normalizedEmail);

                if (account == null)
                    return NotFound("Account not found.");

                var verifiedOtp = await _context.OtpCodes
                    .Where(o => o.AccountId == account.Id && o.IsUsed)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                if (verifiedOtp == null)
                    return BadRequest("OTP not verified. Please verify your OTP first.");

                if (verifiedOtp.CreatedAt < DateTime.UtcNow.AddMinutes(-10))
                    return BadRequest("Reset session expired. Please request a new OTP.");

                if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                    return BadRequest("Password must be at least 6 characters.");

                var hasher = new PasswordHasher<Account>();
                account.PasswordHash = hasher.HashPassword(account, request.NewPassword);
                account.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok("Password reset successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


    }
}
