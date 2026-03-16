using TaskManagement.Data;
using TaskManagement.DTOs.Timelog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TimelogController : ControllerBase
    {
        private readonly AccountDbContext _context;
        public TimelogController(AccountDbContext context)
        {
            _context = context;
        }

        // GET all logs for a task
        [HttpGet("GetTaskLogs/{taskId}")]
        public async Task<IActionResult> GetTaskLogs(int taskId)
        {
            try
            {
                var logs = await _context.TimeLogs
                    .Where(l => l.TaskId == taskId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new TimelogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET all logs for a user
        [HttpGet("GetUserLogs/{accountId}")]
        public async Task<IActionResult> GetUserLogs(int accountId)
        {
            try
            {
                var logs = await _context.TimeLogs
                    .Where(l => l.AccountId == accountId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new TimelogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET all logs in the system
        [HttpGet("GetAllLogs")]
        public async Task<IActionResult> GetAllLogs()
        {
            try
            {
                var logs = await _context.TimeLogs
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new TimelogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET logs by action type
        [HttpGet("GetLogsByAction")]
        public async Task<IActionResult> GetLogsByAction([FromQuery] string action)
        {
            try
            {
                var logs = await _context.TimeLogs
                    .Where(l => l.Action == action)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new TimelogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET logs by date range
        [HttpGet("GetLogsByDateRange")]
        public async Task<IActionResult> GetLogsByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            try
            {
                var logs = await _context.TimeLogs
                    .Where(l => l.CreatedAt >= from && l.CreatedAt <= to)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new TimelogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}