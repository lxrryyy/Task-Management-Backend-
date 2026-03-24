using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Data;
using TaskManagement.Services;
using TaskManagement.Models;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalendarController : ControllerBase
    {
        private readonly AccountDbContext _context;

        public CalendarController(AccountDbContext context)
        {
            _context = context;
        }

        [HttpGet("range")]
        public async Task<IActionResult> GetCalendarRange(
            [FromQuery] int userId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int? projectId = null)
        {
            try
            {
                var user = await _context.Accounts.FindAsync(userId);
                if (user == null)
                    return NotFound("User account not found.");

                if (endDate < startDate)
                    return BadRequest("endDate must be on or after startDate.");

                var from = startDate.Date;
                var to = endDate.Date.AddDays(1).AddTicks(-1); // end of endDate day

                var baseQuery = _context.Tasks
                    .Where(t => !t.IsDeleted
                        && t.StatusId != 4 
                        && t.StatusId!= 3 
                        && t.StartDate <= to
                        && t.DueDate >= from);

                if (projectId.HasValue)
                {
                    var isMember = user.Role == "Admin"
                        || await _context.ProjectMembers
                            .AnyAsync(m => m.ProjectId == projectId.Value
                                       && m.AccountId == userId
                                       && !m.IsDeleted);

                    if (!isMember)
                        return StatusCode(403, "You are not a member of this project.");

                    baseQuery = baseQuery.Where(t => t.ProjectId == projectId.Value);
                }
                else
                {
                    if (user.Role != "Admin")
                    {
                        var userProjectIds = await _context.ProjectMembers
                            .Where(m => m.AccountId == userId && !m.IsDeleted)
                            .Select(m => m.ProjectId)
                            .ToListAsync();

                        baseQuery = baseQuery.Where(t => userProjectIds.Contains(t.ProjectId));
                    }
                }

                var calendarTasks = await baseQuery
                    .Select(t => new
                    {
                        t.Id,
                        t.Title,
                        t.Description,
                        t.StatusId,
                        StatusName = t.Status.Name,
                        t.PriorityId,
                        PriorityName = t.Priority != null ? t.Priority.Name : null,
                        t.ProjectId,
                        ProjectName = _context.Projects
                        .Where(p => p.Id == t.ProjectId)
                        .Select(p => p.Name)
                        .FirstOrDefault(),
                        t.ParentTaskId,
                        t.StoryPoints,
                        t.StartDate,
                        t.DueDate,
                        t.CreatedAt,
                        t.UpdatedAt,
                        AssigneeIds = t.Assignments
                                            .Where(a => !a.IsDeleted)
                                            .Select(a => a.AccountId)
                                            .ToList(),
                        IsAssignedToMe = t.Assignments
                                            .Any(a => a.AccountId == userId && !a.IsDeleted)
                    })
                    .OrderBy(t => t.DueDate)
                    .ToListAsync();

                var myAssignedTaskIds = await _context.TaskAssignments
                    .Where(a => a.AccountId == userId && !a.IsDeleted)
                    .Select(a => a.TaskId)
                    .ToListAsync();

                var myProjectRoles = await _context.ProjectMembers
                    .Where(m => m.AccountId == userId && !m.IsDeleted)
                    .Select(m => new { m.ProjectId, m.Role })
                    .ToListAsync();

                var privilegedProjectIds = myProjectRoles
                    .Where(r => r.Role == "Project Manager"
                             || r.Role == "Scrum Master"
                             || r.Role == "Project Manager - Scrum Master")
                    .Select(r => r.ProjectId)
                    .ToHashSet();

                var todoQuery = _context.Tasks
                    .Where(t => !t.IsDeleted
                        && t.StartDate <= to
                        && t.DueDate >= from
                        && (
                            myAssignedTaskIds.Contains(t.Id)                   
                            || (user.Role == "Admin")                           
                            || privilegedProjectIds.Contains(t.ProjectId)       
                        ));

                if (projectId.HasValue)
                    todoQuery = todoQuery.Where(t => t.ProjectId == projectId.Value);

                var todoTasks = await todoQuery
                    .Select(t => new
                    {
                        t.Id,
                        t.Title,
                        t.StatusId,
                        StatusName = t.Status.Name,
                        t.PriorityId,
                        PriorityName = t.Priority != null ? t.Priority.Name : null,
                        t.ProjectId,
                        ProjectName = _context.Projects
                            .Where(p => p.Id == t.ProjectId)
                            .Select(p => p.Name)
                            .FirstOrDefault(),
                        t.ParentTaskId,
                        t.StoryPoints,
                        t.StartDate,
                        t.DueDate,
                        Depth = t.ParentTaskId == null ? 0 : 1  
                    })
                    .OrderBy(t => t.DueDate)
                    .ThenBy(t => t.ParentTaskId)
                    .ToListAsync();

                var taskDepthMap = new Dictionary<int, int>();
                var parentMap = todoTasks.ToDictionary(t => t.Id, t => t.ParentTaskId);

                int GetDepth(int id)
                {
                    if (taskDepthMap.TryGetValue(id, out var cached)) return cached;
                    if (!parentMap.TryGetValue(id, out var parentId) || parentId == null)
                    {
                        taskDepthMap[id] = 0;
                        return 0;
                    }
                    var depth = 1 + GetDepth(parentId.Value);
                    taskDepthMap[id] = depth;
                    return depth;
                }

                foreach (var t in todoTasks) GetDepth(t.Id);

                var dayGroups = new List<object>();
                for (var day = from; day <= endDate.Date; day = day.AddDays(1))
                {
                    var dayStart = day;
                    var dayEnd = day.AddDays(1).AddTicks(-1);

                    var dayCalendarTasks = calendarTasks
                        .Where(t => t.StartDate <= dayEnd && t.DueDate >= dayStart)
                        .ToList();

                    var dayTodoTasks = todoTasks
                        .Where(t => t.StartDate <= dayEnd && t.DueDate >= dayStart)
                        .Select(t => new
                        {
                            t.Id,
                            t.Title,
                            t.StatusId,
                            t.StatusName,
                            t.PriorityId,
                            t.PriorityName,
                            t.ProjectId,
                            t.ProjectName,
                            t.ParentTaskId,
                            t.StoryPoints,
                            t.StartDate,
                            t.DueDate,
                            Depth = taskDepthMap.TryGetValue(t.Id, out var d) ? d : 0
                        })
                        .ToList();

                    dayGroups.Add(new
                    {
                        Date = day.ToString("yyyy-MM-dd"),
                        CalendarTaskCount = dayCalendarTasks.Count,
                        CalendarTasks = dayCalendarTasks,
                        TodoCount = dayTodoTasks.Count,       
                        TodoItems = dayTodoTasks              
                    });
                }

                return Ok(new
                {
                    UserId = userId,
                    ProjectId = projectId,
                    RangeStart = from.ToString("yyyy-MM-dd"),
                    RangeEnd = endDate.Date.ToString("yyyy-MM-dd"),
                    TotalDays = (endDate.Date - from).Days + 1,
                    TotalCalendarTasks = calendarTasks.Count,
                    TotalTodoItems = todoTasks.Count,
                    Days = dayGroups
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("day")]
        public async Task<IActionResult> GetCalendarDay(
            [FromQuery] int userId,
            [FromQuery] DateTime date,
            [FromQuery] int? projectId = null)
        {
            try
            {
                var user = await _context.Accounts.FindAsync(userId);
                if (user == null)
                    return NotFound("User account not found.");

                var dayStart = date.Date;
                var dayEnd = date.Date.AddDays(1).AddTicks(-1);

                var myAssignedTaskIds = await _context.TaskAssignments
                    .Where(a => a.AccountId == userId && !a.IsDeleted)
                    .Select(a => a.TaskId)
                    .ToListAsync();

                var myProjectRoles = await _context.ProjectMembers
                    .Where(m => m.AccountId == userId && !m.IsDeleted)
                    .Select(m => new { m.ProjectId, m.Role })
                    .ToListAsync();

                var privilegedProjectIds = myProjectRoles
                    .Where(r => r.Role == "Project Manager"
                             || r.Role == "Scrum Master"
                             || r.Role == "Project Manager - Scrum Master")
                    .Select(r => r.ProjectId)
                    .ToHashSet();

                if (projectId.HasValue)
                {
                    var isMember = user.Role == "Admin"
                        || myProjectRoles.Any(r => r.ProjectId == projectId.Value);

                    if (!isMember)
                        return StatusCode(403, "You are not a member of this project.");
                }

                var todoQuery = _context.Tasks
                    .Where(t => !t.IsDeleted && 
                    t.StatusId != 4 
                    && t.StatusId != 3
                        && t.StartDate <= dayEnd
                        && t.DueDate >= dayStart
                        && (
                            myAssignedTaskIds.Contains(t.Id)
                            || user.Role == "Admin"
                            || privilegedProjectIds.Contains(t.ProjectId)
                        ));

                if (projectId.HasValue)
                    todoQuery = todoQuery.Where(t => t.ProjectId == projectId.Value);
                else if (user.Role != "Admin")
                {
                    var userProjectIds = myProjectRoles.Select(r => r.ProjectId).ToList();
                    todoQuery = todoQuery.Where(t => userProjectIds.Contains(t.ProjectId));
                }

                var tasks = await todoQuery
                    .Select(t => new
                    {
                        t.Id,
                        t.Title,
                        t.Description,
                        t.StatusId,
                        StatusName = t.Status.Name,
                        t.PriorityId,
                        PriorityName = t.Priority != null ? t.Priority.Name : null,
                        t.ProjectId,
                        ProjectName = _context.Projects
                            .Where(p => p.Id == t.ProjectId)
                            .Select(p => p.Name)
                            .FirstOrDefault(),
                        t.ParentTaskId,
                        t.StoryPoints,
                        t.StartDate,
                        t.DueDate,
                        t.CreatedAt,
                        t.UpdatedAt,
                        AssigneeIds = t.Assignments
                                         .Where(a => !a.IsDeleted)
                                         .Select(a => a.AccountId)
                                         .ToList()
                    })
                    .OrderBy(t => t.ParentTaskId)
                    .ThenBy(t => t.DueDate)
                    .ToListAsync();

                var taskDepthMap = new Dictionary<int, int>();
                var parentMap = tasks.ToDictionary(t => t.Id, t => t.ParentTaskId);

                int GetDepth(int id)
                {
                    if (taskDepthMap.TryGetValue(id, out var cached)) return cached;
                    if (!parentMap.TryGetValue(id, out var parentId) || parentId == null)
                    {
                        taskDepthMap[id] = 0;
                        return 0;
                    }
                    var depth = 1 + GetDepth(parentId.Value);
                    taskDepthMap[id] = depth;
                    return depth;
                }

                foreach (var t in tasks) GetDepth(t.Id);

                var rootTasks = tasks.Where(t => t.ParentTaskId == null).ToList();

                List<object> BuildTree(int? parentId)
                {
                    return tasks
                        .Where(t => t.ParentTaskId == parentId)
                        .Select(t => (object)new
                        {
                            t.Id,
                            t.Title,
                            t.Description,
                            t.StatusId,
                            t.StatusName,
                            t.PriorityId,
                            t.PriorityName,
                            t.ProjectId,
                            t.ProjectName,
                            t.ParentTaskId,
                            t.StoryPoints,
                            t.StartDate,
                            t.DueDate,
                            t.CreatedAt,
                            t.UpdatedAt,
                            t.AssigneeIds,
                            Depth = taskDepthMap.TryGetValue(t.Id, out var d) ? d : 0,
                            Subtasks = BuildTree(t.Id)
                        })
                        .ToList();
                }

                var tree = BuildTree(null);

                return Ok(new
                {
                    UserId = userId,
                    Date = date.Date.ToString("yyyy-MM-dd"),
                    ProjectId = projectId,
                    TotalCount = tasks.Count,
                    RootCount = rootTasks.Count,
                    TodoTree = tree   
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }
    }
}