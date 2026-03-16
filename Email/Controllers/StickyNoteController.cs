using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Data;
using TaskManagement.DTOs.StickyNote;
using TaskManagement.Models;
using TaskManagement.Services;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StickyNoteController : ControllerBase
    {
        private readonly AccountDbContext _context;
        private static DateTime PhTime =>
         TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
        public StickyNoteController(AccountDbContext context)
        {
            _context = context;
        }

        // GET all notes for an account (pinned first)
        [HttpGet("GetMyNotes/{accountId}")]
        public async Task<IActionResult> GetMyNotes(int accountId)
        {
            try
            {
                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                    return NotFound("Account not found.");

                var notes = await _context.StickyNotes
                    .Where(n => n.AccountId == accountId && !n.IsDeleted)
                    .OrderByDescending(n => n.IsPinned)
                    .ThenByDescending(n => n.UpdatedAt)
                    .Select(n => new StickyNoteResponseDTO
                    {
                        Id = n.Id,
                        AccountId = n.AccountId,
                        Content = n.Content,
                        IsPinned = n.IsPinned,
                        CreatedAt = n.CreatedAt,
                        UpdatedAt = n.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(notes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET single note
        [HttpGet("GetNoteById/{id}")]
        public async Task<IActionResult> GetNoteById(int id, [FromQuery] int accountId)
        {
            try
            {
                var note = await _context.StickyNotes
                    .Where(n => n.Id == id && n.AccountId == accountId && !n.IsDeleted)
                    .Select(n => new StickyNoteResponseDTO
                    {
                        Id = n.Id,
                        AccountId = n.AccountId,
                        Content = n.Content,
                        IsPinned = n.IsPinned,
                        CreatedAt = n.CreatedAt,
                        UpdatedAt = n.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (note == null)
                    return NotFound("Note not found.");

                return Ok(note);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST create note
        [HttpPost("CreateNote/{accountId}")]
        public async Task<IActionResult> CreateNote(int accountId, [FromBody] CreateStickyNoteDTO dto)
        {
            try
            {
                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                    return NotFound("Account not found.");

                if (string.IsNullOrWhiteSpace(dto.Content))
                    return BadRequest("Content cannot be empty.");

                var note = new StickyNote
                {
                    AccountId = accountId,
                    Content = dto.Content,
                    IsPinned = false,
                    CreatedAt = PhTime,
                    UpdatedAt = PhTime
                };

                _context.StickyNotes.Add(note);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetNoteById), new { id = note.Id }, new StickyNoteResponseDTO
                {
                    Id = note.Id,
                    AccountId = note.AccountId,
                    Content = note.Content,
                    IsPinned = note.IsPinned,
                    CreatedAt = note.CreatedAt,
                    UpdatedAt = note.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PATCH update note content / pin
        [HttpPatch("UpdateNote/{id}")]
        public async Task<IActionResult> UpdateNote(int id, [FromQuery] int accountId, [FromBody] UpdateStickyNoteDTO dto)
        {
            try
            {
                var note = await _context.StickyNotes
                    .FirstOrDefaultAsync(n => n.Id == id && n.AccountId == accountId && !n.IsDeleted);

                if (note == null)
                    return NotFound("Note not found.");

                if (dto.Content != null)
                {
                    if (string.IsNullOrWhiteSpace(dto.Content))
                        return BadRequest("Content cannot be empty.");
                    note.Content = dto.Content;
                }

                if (dto.IsPinned.HasValue)
                    note.IsPinned = dto.IsPinned.Value;

                note.UpdatedAt = PhTime;

                await _context.SaveChangesAsync();

                return Ok(new StickyNoteResponseDTO
                {
                    Id = note.Id,
                    AccountId = note.AccountId,
                    Content = note.Content,
                    IsPinned = note.IsPinned,
                    CreatedAt = note.CreatedAt,
                    UpdatedAt = note.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


        // DELETE soft delete note
        [HttpDelete("DeleteNote/{id}")]
        public async Task<IActionResult> DeleteNote(int id, [FromQuery] int accountId)
        {
            try
            {
                var note = await _context.StickyNotes
                    .FirstOrDefaultAsync(n => n.Id == id && n.AccountId == accountId && !n.IsDeleted);

                if (note == null)
                    return NotFound("Note not found.");

                note.IsDeleted = true;
                note.DeletedAt = PhTime;
                note.UpdatedAt = PhTime;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Note deleted successfully.",
                    id = note.Id,
                    deletedAt = note.DeletedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}