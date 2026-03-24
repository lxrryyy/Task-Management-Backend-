using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Data;
using TaskManagement.Models;
using TaskManagement.DTOs.Comment;
namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskCommentController : ControllerBase
    {
        private readonly AccountDbContext _context;
        private static DateTime PhTime =>
             TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
        public TaskCommentController(AccountDbContext context)
        {
            _context = context;
        }

        [HttpGet("GetCommentsByTask/{taskId}")]
        public async Task<IActionResult> GetCommentsByTask(int taskId)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(taskId);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var comments = await _context.TaskComments
                    .Where(c => c.TaskId == taskId && !c.IsDeleted)
                    .Select(c => new
                    {
                        c.Id,
                        c.TaskId,
                        c.AccountId,
                        AccountName = _context.Accounts
                            .Where(a => a.Id == c.AccountId)
                            .Select(a => a.Name)
                            .FirstOrDefault(),
                        c.Content,
                        c.CreatedAt,
                        c.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(comments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("CreateComment")]
        public async Task<IActionResult> CreateComment([FromBody] CreateCommentDTO dto, [FromQuery] int accountId)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(dto.TaskId);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                    return NotFound("Account not found.");

                var projectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == accountId && !m.IsDeleted);

                if (account.Role != "Admin" && projectMember == null)
                    return StatusCode(403, "You are not a member of this project.");

                var comment = new TaskComment
                {
                    TaskId = dto.TaskId,
                    AccountId = accountId,
                    Content = dto.Content,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.TaskComments.Add(comment);
                _context.AuditLogs.Add(new AuditLog
                {
                    TaskId = dto.TaskId,
                    AccountId = accountId,
                    Action = "CREATED",
                    NewValue = dto.Content,
                    Note = $"User {account.Name} leaves a comment on task {dto.TaskId}.",
                    CreatedAt = PhTime
                });

                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCommentsByTask), new { taskId = dto.TaskId }, new
                {
                    comment.Id,
                    comment.TaskId,
                    comment.AccountId,
                    comment.Content,
                    comment.CreatedAt,
                    comment.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPatch("UpdateComment/{id}")]
        public async Task<IActionResult> UpdateComment(int id, [FromBody] UpdateCommentDTO dto, [FromQuery] int accountId)
        {
            try
            {
                var comment = await _context.TaskComments.FindAsync(id);
                if (comment == null || comment.IsDeleted)
                    return NotFound("Comment not found.");

                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                    return NotFound("Account not found.");

                if (comment.AccountId != accountId && account.Role != "Admin")
                    return StatusCode(403, "You can only edit your own comments.");

                comment.Content = dto.Content;
                comment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("DeleteComment/{id}")]
        public async Task<IActionResult> DeleteComment(int id, [FromQuery] int accountId)
        {
            try
            {
                var comment = await _context.TaskComments.FindAsync(id);
                if (comment == null || comment.IsDeleted)
                    return NotFound("Comment not found.");

                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                    return NotFound("Account not found.");

                var task = await _context.Tasks.FindAsync(comment.TaskId);

                var projectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == accountId && !m.IsDeleted);

                var accountRole = account.Role == "Admin" ? "Admin" : projectMember?.Role;

                if (comment.AccountId != accountId &&
                    accountRole != "Admin" &&
                    accountRole != "ProjectManager" &&
                    accountRole != "ScrumMaster" &&
                    accountRole != "ProjectManager-ScrumMaster")
                    return StatusCode(403, "You do not have permission to delete this comment.");

                comment.IsDeleted = true;
                comment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}