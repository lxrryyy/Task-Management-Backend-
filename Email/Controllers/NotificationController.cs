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

        [HttpPatch("MarkAsRead/{id}")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(id);
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

        [HttpPatch("MarkAllAsRead/{accountId}")]
        public async Task<IActionResult> MarkAllAsRead(int accountId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.AccountId == accountId && !n.IsRead)
                    .ToListAsync();

                foreach (var n in notifications)
                    n.IsRead = true;

                await _context.SaveChangesAsync();

                return Ok(new { message = $"{notifications.Count} notifications marked as read." });
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
