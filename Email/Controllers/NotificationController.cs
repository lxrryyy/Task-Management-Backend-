using TaskManagement.Data;
using TaskManagement.DTOs.Notification;
using TaskManagement.Models;
using TaskManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
	public class NotificationController : ControllerBase
    {
        private readonly AccountDbContext _context;
        private readonly IEmailService _emailService;
		private static DateTime PhTime => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
		public NotificationController(AccountDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET all notifications for a user
        [HttpGet("GetNotifications/{accountId}")]
        public async Task<IActionResult> GetNotifications(int accountId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.AccountId == accountId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PATCH mark notification as read
        [HttpPatch("MarkAsRead/{notificationId}")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(notificationId);

                if (notification == null)
                    return NotFound("Notification not found.");

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PATCH mark all notifications as read
        [HttpPatch("MarkAllAsRead/{accountId}")]
        public async Task<IActionResult> MarkAllAsRead(int accountId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.AccountId == accountId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST send task assigned notification
        [HttpPost("SendTaskAssigned/{taskId}")]
        public async Task<IActionResult> SendTaskAssigned(int taskId, [FromQuery] int adminId)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(taskId);

                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                // Get all assignees
                var assignees = await _context.TaskAssignments
                    .Where(a => a.TaskId == taskId)
                    .ToListAsync();

                if (!assignees.Any())
                    return BadRequest("No assignees found for this task.");

                foreach (var assignment in assignees)
                {
                    var account = await _context.Accounts.FindAsync(assignment.AccountId);

                    if (account == null) continue;

                    // Save notification to db
                    _context.Notifications.Add(new Notification
                    {
                        AccountId = assignment.AccountId,
                        TaskId = taskId,
                        Message = $"You have been assigned to task: {task.Title}",
                        Type = "TaskAssigned",
                        IsRead = false,
                        CreatedAt = PhTime
                    });

                    // Send email
                    await _emailService.SendTaskAssignedAsync(account.Email, task.Title, taskId);
                }

                await _context.SaveChangesAsync();
                return Ok("Notifications sent successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST send status changed notification
        [HttpPost("SendStatusChanged/{taskId}")]
        public async Task<IActionResult> SendStatusChanged(int taskId, [FromQuery] string newStatus)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(taskId);

                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var assignees = await _context.TaskAssignments
                    .Where(a => a.TaskId == taskId)
                    .ToListAsync();

                foreach (var assignment in assignees)
                {
                    var account = await _context.Accounts.FindAsync(assignment.AccountId);

                    if (account == null) continue;

                    _context.Notifications.Add(new Notification
                    {
                        AccountId = assignment.AccountId,
                        TaskId = taskId,
                        Message = $"Task '{task.Title}' status changed to: {newStatus}",
                        Type = "StatusChanged",
                        IsRead = false,
                        CreatedAt = PhTime
                    });

                    await _emailService.SendStatusChangedAsync(account.Email, task.Title, newStatus);
                }

                await _context.SaveChangesAsync();
                return Ok("Notifications sent successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST send deadline reminder
        [HttpPost("SendDeadlineReminder/{taskId}")]
        public async Task<IActionResult> SendDeadlineReminder(int taskId)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(taskId);

                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                if (task.DueDate == null)
                    return BadRequest("Task has no due date.");

                var assignees = await _context.TaskAssignments
                    .Where(a => a.TaskId == taskId)
                    .ToListAsync();

                foreach (var assignment in assignees)
                {
                    var account = await _context.Accounts.FindAsync(assignment.AccountId);

                    if (account == null) continue;

                    _context.Notifications.Add(new Notification
                    {
                        AccountId = assignment.AccountId,
                        TaskId = taskId,
                        Message = $"Reminder: Task '{task.Title}' is due on {task.DueDate:MMMM dd, yyyy}",
                        Type = "DeadlineReminder",
                        IsRead = false,
                        CreatedAt = PhTime
                    });

                    await _emailService.SendDeadlineReminderAsync(account.Email, task.Title, task.DueDate.Value);
                }

                await _context.SaveChangesAsync();
                return Ok("Deadline reminders sent successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST set notification settings
        [HttpPost("SetNotificationSettings")]
        public async Task<IActionResult> SetNotificationSettings([FromBody] NotificationSettingDTO dto)
        {
            try
            {
                // Save settings to db as a notification record for reference
                _context.Notifications.Add(new Notification
                {
                    AccountId = dto.AccountId,
                    Message = $"Settings: EmailOnAssign={dto.EmailOnAssign}, " +
                              $"EmailOnStatusChange={dto.EmailOnStatusChange}, " +
                              $"ReminderDaysBefore={dto.DeadlineReminderDaysBefore}",
                    Type = "Settings",
                    IsRead = true,
                    CreatedAt = PhTime
                });

                await _context.SaveChangesAsync();
                return Ok("Notification settings saved.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}