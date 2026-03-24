using Microsoft.AspNetCore.Mvc;
using TaskManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly AccountDbContext _context;

        public NotificationController(AccountDbContext context)
        {
            _context = context;
        }

        [HttpGet("GetNotifications/{accountId}")]
        public async Task<IActionResult> GetNotifications(int accountId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.AccountId == accountId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.Id,
                        n.AccountId,
                        n.ProjectId,
                        n.TaskId,
                        n.Message,
                        n.IsRead,
                        n.CreatedAt
                    })
                    .ToListAsync();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("GetUnreadNotifications/{accountId}")]
        public async Task<IActionResult> GetUnreadNotifications(int accountId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.AccountId == accountId && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.Id,
                        n.AccountId,
                        n.ProjectId,
                        n.TaskId,
                        n.Message,
                        n.IsRead,
                        n.CreatedAt
                    })
                    .ToListAsync();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                    return NotFound(new { message = "Notification not found." });

                if (notification.IsRead)
                    return Ok(new { message = "Notification is already marked as read." });

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Notification marked as read.",
                    notificationId = notification.Id,
                    isRead = notification.IsRead
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PUT api/notifications/read-all?accountId=1
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] int accountId)
        {
            try
            {
                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                    return NotFound(new { message = "Account not found." });

                var notifications = await _context.Notifications
                    .Where(n => n.AccountId == accountId && !n.IsRead)
                    .ToListAsync();

                if (!notifications.Any())
                    return Ok(new { message = "No unread notifications found.", markedAsRead = 0 });

                foreach (var n in notifications)
                    n.IsRead = true;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"{notifications.Count} notification(s) marked as read.",
                    markedAsRead = notifications.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("DeleteNotification/{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                    return NotFound("Notification not found.");

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Notification deleted." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
