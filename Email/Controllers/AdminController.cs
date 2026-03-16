using TaskManagement.Data;
using TaskManagement.DTOs.Admin;
using TaskManagement.DTOs.Task;
using TaskManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AccountDbContext _context;
		private static DateTime PhTime =>
	        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
		public AdminController(AccountDbContext context)
        {
            _context = context;
        }

        // Force update task status
        [HttpPatch("ForceUpdateStatus/{taskId}")]
        public async Task<IActionResult> ForceUpdateStatus(int taskId, [FromBody] ForceUpdateStatusDTO dto, [FromQuery] int adminId)
        {
            try
            {
                var admin = await _context.Accounts.FindAsync(adminId);
                if (admin == null || admin.Role != "Admin")
                    return StatusCode(403, "Access denied. Admins only.");

                var task = await _context.Tasks.FindAsync(taskId);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                // Validate StatusId
                var statusExists = await _context.TaskStatuses.AnyAsync(s => s.Id == dto.StatusId);
                if (!statusExists)
                    return BadRequest("Invalid StatusId.");

                var oldStatusId = task.StatusId;
                task.StatusId = dto.StatusId; 
                task.UpdatedAt = PhTime;

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = taskId,
                    AccountId = adminId,
                    Action = "StatusChanged",
                    OldValue = oldStatusId.ToString(),
                    NewValue = dto.StatusId.ToString(),
                    Note = dto.Note ?? "Force updated by admin"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Change task priority
        [HttpPatch("ChangePriority/{taskId}")]
        public async Task<IActionResult> ChangePriority(int taskId, [FromQuery] int priorityId, [FromQuery] int adminId)
        {
            try
            {
                var admin = await _context.Accounts.FindAsync(adminId);
                if (admin == null || admin.Role != "Admin")
                    return StatusCode(403, "Access denied. Admins only.");

                var task = await _context.Tasks.FindAsync(taskId);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                // Validate PriorityId
                var priorityExists = await _context.TaskPriorities.AnyAsync(p => p.Id == priorityId);
                if (!priorityExists)
                    return BadRequest("Invalid PriorityId.");

                var oldPriorityId = task.PriorityId;
                task.PriorityId = priorityId; // 👈
                task.UpdatedAt = PhTime;

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = taskId,
                    AccountId = adminId,
                    Action = "PriorityChanged",
                    OldValue = oldPriorityId.ToString(),
                    NewValue = priorityId.ToString(),
                    Note = "Priority changed by admin"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Update task deadline
        [HttpPatch("UpdateDeadline/{taskId}")]
        public async Task<IActionResult> UpdateDeadline(int taskId, [FromQuery] DateTime dueDate, [FromQuery] int adminId)
        {
            try
            {
                var admin = await _context.Accounts.FindAsync(adminId);
                if (admin == null || admin.Role != "Admin")
                    return StatusCode(403, "Access denied. Admins only.");

                var task = await _context.Tasks.FindAsync(taskId);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var oldDueDate = task.DueDate?.ToString() ?? "None";
                task.DueDate = dueDate;
                task.UpdatedAt = PhTime;

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = taskId,
                    AccountId = adminId,
                    Action = "DeadlineUpdated",
                    OldValue = oldDueDate,
                    NewValue = dueDate.ToString(),
                    Note = "Deadline updated by admin"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Reassign task
        [HttpPatch("ReassignTask/{taskId}")]
        public async Task<IActionResult> ReassignTask(int taskId, [FromBody] AssignTaskDTO dto, [FromQuery] int adminId)
        {
            try
            {
                var admin = await _context.Accounts.FindAsync(adminId);
                if (admin == null || admin.Role != "Admin")
                    return StatusCode(403, "Access denied. Admins only.");

                var task = await _context.Tasks.FindAsync(taskId);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var oldAssignees = await _context.TaskAssignments
                    .Where(a => a.TaskId == taskId)
                    .Select(a => a.AccountId)
                    .ToListAsync();

                var existing = _context.TaskAssignments.Where(a => a.TaskId == taskId);
                _context.TaskAssignments.RemoveRange(existing);

                foreach (var accountId in dto.AssigneeIds)
                {
                    _context.TaskAssignments.Add(new TaskAssignment
                    {
                        TaskId = taskId,
                        AccountId = accountId,
                        AssignedById = adminId,
                        AssignedAt = PhTime
                    });
                }

                task.UpdatedAt = PhTime;

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = taskId,
                    AccountId = adminId,
                    Action = "Reassigned",
                    OldValue = string.Join(", ", oldAssignees),
                    NewValue = string.Join(", ", dto.AssigneeIds),
                    Note = "Task reassigned by admin"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Update task permission
        [HttpPost("UpdatePermission")]
        public async Task<IActionResult> UpdatePermission([FromBody] UpdatePermissionDTO dto, [FromQuery] int adminId)
        {
            try
            {
                var admin = await _context.Accounts.FindAsync(adminId);
                if (admin == null || admin.Role != "Admin")
                    return StatusCode(403, "Access denied. Admins only.");

                var existing = await _context.TaskPermissions
                    .SingleOrDefaultAsync(p => p.TaskId == dto.TaskId && p.AccountId == dto.AccountId);

                if (existing == null)
                {
                    _context.TaskPermissions.Add(new TaskPermission
                    {
                        TaskId = dto.TaskId,
                        AccountId = dto.AccountId,
                        CanView = dto.CanView,
                        CanEdit = dto.CanEdit,
                        CanDelete = dto.CanDelete,
                        CanComment = dto.CanComment,
                        CreatedAt = PhTime,
                        UpdatedAt = PhTime
                    });
                }
                else
                {
                    existing.CanView = dto.CanView;
                    existing.CanEdit = dto.CanEdit;
                    existing.CanDelete = dto.CanDelete;
                    existing.CanComment = dto.CanComment;
                    existing.UpdatedAt = PhTime;
                }

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = dto.TaskId,
                    AccountId = adminId,
                    Action = "PermissionUpdated",
                    Note = $"Permission updated for account {dto.AccountId} by admin"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Get task permissions
        [HttpGet("GetTaskPermissions/{taskId}")]
        public async Task<IActionResult> GetTaskPermissions(int taskId)
        {
            try
            {
                var permissions = await _context.TaskPermissions
                    .Where(p => p.TaskId == taskId)
                    .ToListAsync();

                return Ok(permissions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}