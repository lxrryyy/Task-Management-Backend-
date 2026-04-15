using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Data;
using TaskManagement.Models;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase     
    {
        private readonly AccountDbContext _context;
        private static DateTime PhTime =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
        public DashboardController(AccountDbContext context)
        {
            _context = context;
        }

        [HttpGet("GetDashboardAdminStats")]
        public async Task<IActionResult> GetDashboardAdminStats()
        {
            try
            {
                var totalUsers = await _context.Accounts
                    .Where(a => a.isActive)
                    .CountAsync();

                var totalProjects = await _context.Projects
                    .Where(p => !p.IsDeleted)
                    .CountAsync();

                var overdueTasks = await _context.Tasks
                    .Where(t => !t.IsDeleted && t.DueDate < PhTime && t.StatusId != 4)
                    .CountAsync();

                var deactivatedUsers = await _context.Accounts
                    .Where(a => !a.isActive)
                    .CountAsync();
                return Ok(new
                {
                    totalUsers,
                    totalProjects,
                    overdueTasks,
                    deactivatedUsers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("GetDashboardUserStats")]
        public async Task<IActionResult> GetDashboardUserStats()
        {
            try
            {
                var totalUsers = await _context.Accounts
                    .Where(a => a.isActive)
                    .CountAsync();

                var totalProjects = await _context.Projects
                    .Where(p => !p.IsDeleted)
                    .CountAsync();

                var overdueTasks = await _context.Tasks
                    .Where(t => !t.IsDeleted && t.DueDate < PhTime && t.StatusId != 4)
                    .CountAsync();

                var completedTasks = await _context.Tasks
                    .Where(t => !t.IsDeleted && t.StatusId == 4)
                    .CountAsync();

                return Ok(new
                {
                    totalUsers,
                    totalProjects,
                    overdueTasks,
                    completedTasks
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet("MyProjectsAndTasks")]   // return all projects and tasks with subtasks
        public async Task<IActionResult> GetMyProjectsAndTasks([FromQuery] int requesterId)
        {
            try
            {
                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                IQueryable<Project> projectQuery = _context.Projects
                    .Where(p => !p.IsDeleted);

                if (requester.Role != "Admin")
                {
                    projectQuery = projectQuery.Where(p =>
                        _context.ProjectMembers.Any(m =>
                            m.ProjectId == p.Id &&
                            m.AccountId == requesterId &&
                            !m.IsDeleted));
                }

                var projects = await projectQuery
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.StatusId,
                    StatusName = p.Status.Name,
                    p.CreatedAt,
                    p.UpdatedAt,
                    RootTasks = _context.Tasks
                        .Where(t => t.ProjectId == p.Id && !t.IsDeleted && t.ParentTaskId == null &&
                            (requester.Role == "Admin" ||
                             _context.TaskAssignments.Any(a => a.TaskId == t.Id && a.AccountId == requesterId && !a.IsDeleted) ||
                             _context.ProjectMembers.Any(m => m.ProjectId == p.Id && m.AccountId == requesterId && !m.IsDeleted &&
                                 (m.Role == "Project Manager" || m.Role == "Scrum Master" || m.Role == "Project Manager - Scrum Master"))))
                        .Select(t => new
                        {
                            t.Id,
                            t.Title,
                            t.Description,
                            t.StatusId,
                            StatusName = t.Status.Name,
                            t.PriorityId,
                            PriorityName = t.Priority.Name,
                            t.StoryPoints,
                            t.StartDate,
                            t.DueDate,
                            t.CreatedAt,
                            t.UpdatedAt,
                            AssigneeIds = t.Assignments.Where(a => !a.IsDeleted).Select(a => a.AccountId).ToList()
                        }).ToList()
                })
                .ToListAsync();

                var finalResult = new List<object>();
                foreach (var project in projects)
                {
                    var tasksWithSubtasks = new List<object>();
                    foreach (var task in project.RootTasks)
                    {
                        tasksWithSubtasks.Add(new
                        {
                            task.Id,
                            task.Title,
                            task.Description,
                            task.StatusId,
                            task.StatusName,
                            task.PriorityId,
                            task.PriorityName,
                            task.StoryPoints,
                            task.StartDate,
                            task.DueDate,
                            task.CreatedAt,
                            task.UpdatedAt,
                            task.AssigneeIds,
                            Subtasks = await GetSubtasksRecursive(task.Id)
                        });
                    }

                    finalResult.Add(new
                    {
                        project.Id,
                        project.Name,
                        project.Description,
                        project.StatusId,
                        project.StatusName,
                        project.CreatedAt,
                        project.UpdatedAt,
                        Tasks = tasksWithSubtasks
                    });
                }

                return Ok(finalResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<List<object>> GetSubtasksRecursive(int parentId)
        {
            var subtasks = await _context.Tasks
                .Where(s => s.ParentTaskId == parentId && !s.IsDeleted)
                .Select(s => new
                {
                    s.Id,
                    s.Title,
                    s.Description,
                    s.StatusId,
                    StatusName = s.Status.Name,
                    s.PriorityId,
                    PriorityName = s.Priority.Name,
                    s.StoryPoints,
                    s.StartDate,
                    s.DueDate,
                    s.CreatedAt,
                    s.UpdatedAt,
                    AssigneeIds = s.Assignments.Where(a => !a.IsDeleted).Select(a => a.AccountId).ToList()
                })
                .ToListAsync();

            var result = new List<object>();
            foreach (var subtask in subtasks)
            {
                result.Add(new
                {
                    subtask.Id,
                    subtask.Title,
                    subtask.Description,
                    subtask.StatusId,
                    subtask.StatusName,
                    subtask.PriorityId,
                    subtask.PriorityName,
                    subtask.StoryPoints,
                    subtask.StartDate,
                    subtask.DueDate,
                    subtask.CreatedAt,
                    subtask.UpdatedAt,
                    subtask.AssigneeIds,
                    Subtasks = await GetSubtasksRecursive(subtask.Id) 
                });
            }

            return result;
        }

        [HttpGet("ProjectStatusSummary")] // return count and percentage (Active, Completed, Not Started)-PROJECTS
        public async Task<IActionResult> GetProjectStatusSummary([FromQuery] int requesterId)
        {
            try
            {
                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                IQueryable<Project> projectQuery = _context.Projects
                    .Where(p => !p.IsDeleted);

                if (requester.Role != "Admin")
                {
                    projectQuery = projectQuery.Where(p =>
                        _context.ProjectMembers.Any(m =>
                            m.ProjectId == p.Id &&
                            m.AccountId == requesterId &&
                            !m.IsDeleted));
                }

                var total = await projectQuery.CountAsync();

                var grouped = await projectQuery
                    .GroupBy(p => new { p.StatusId, p.Status.Name })
                    .Select(g => new
                    {
                        StatusId = g.Key.StatusId,
                        StatusName = g.Key.Name,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var statuses = await _context.ProjectStatuses
                    .Where(s => s.IsActive)
                    .ToListAsync();

                var result = statuses.Select(s =>
                {
                    var match = grouped.FirstOrDefault(g => g.StatusId == s.Id);
                    var count = match?.Count ?? 0;
                    return new
                    {
                        StatusId = s.Id,
                        StatusName = s.Name,
                        Count = count,
                        Percentage = total == 0 ? 0.0 : Math.Round((double)count / total * 100, 2)
                    };
                });

                return Ok(new
                {
                    TotalProjects = total,
                    Breakdown = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("TaskStatusSummary")] // return count and percentage (Not Started, In Progress, Completed, For Review)-PROJECTS
        public async Task<IActionResult> GetTaskStatusSummary([FromQuery] int requesterId)
        {
            try
            {
                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                IQueryable<TaskItem> taskQuery = _context.Tasks
                    .Where(t => !t.IsDeleted);

                if (requester.Role != "Admin")
                {
                    taskQuery = taskQuery.Where(t =>
                        _context.TaskAssignments.Any(a =>
                            a.TaskId == t.Id &&
                            a.AccountId == requesterId &&
                            !a.IsDeleted) ||
                        _context.ProjectMembers.Any(m =>
                            m.ProjectId == t.ProjectId &&
                            m.AccountId == requesterId &&
                            !m.IsDeleted &&
                            (m.Role == "Project Manager" ||
                             m.Role == "Scrum Master" ||
                             m.Role == "Project Manager - Scrum Master")));
                }

                var total = await taskQuery.CountAsync();

                var grouped = await taskQuery
                    .GroupBy(t => new { t.StatusId, t.Status.Name })
                    .Select(g => new
                    {
                        StatusId = g.Key.StatusId,
                        StatusName = g.Key.Name,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var statuses = await _context.TaskStatuses
                    .Where(s => s.IsActive)
                    .ToListAsync();

                var result = statuses.Select(s =>
                {
                    var match = grouped.FirstOrDefault(g => g.StatusId == s.Id);
                    var count = match?.Count ?? 0;
                    return new
                    {
                        StatusId = s.Id,
                        StatusName = s.Name,
                        Count = count,
                        Percentage = total == 0 ? 0.0 : Math.Round((double)count / total * 100, 2)
                    };
                });

                return Ok(new
                {
                    TotalTasks = total,
                    Breakdown = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("ProjectTaskSummary/{projectId}")] // return count and percentage (Completed, Working On)
        public async Task<IActionResult> GetProjectTaskSummary(int projectId, [FromQuery] int requesterId)
        {
            try
            {
                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
                if (project == null)
                    return NotFound("Project not found.");

                if (requester.Role != "Admin")
                {
                    var isMember = await _context.ProjectMembers
                        .AnyAsync(m => m.ProjectId == projectId && m.AccountId == requesterId && !m.IsDeleted);
                    if (!isMember)
                        return StatusCode(403, "You are not a member of this project.");
                }
                IQueryable<TaskItem> taskQuery = _context.Tasks
                 .Where(t => t.ProjectId == projectId && !t.IsDeleted);

                if (requester.Role != "Admin")
                {
                    var projectMember = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == requesterId && !m.IsDeleted);

                    var isPrivileged = projectMember?.Role == "Project Manager" ||
                                       projectMember?.Role == "Scrum Master" ||
                                       projectMember?.Role == "Project Manager - Scrum Master";

                    if (!isPrivileged)
                    {
                        // members
                        taskQuery = taskQuery.Where(t =>
                            _context.TaskAssignments.Any(a =>
                                a.TaskId == t.Id &&
                                a.AccountId == requesterId &&
                                !a.IsDeleted));
                    }
                }
                var tasks = await taskQuery
                   .Select(t => new { t.StatusId })
                   .ToListAsync();

                var total = tasks.Count;
                var completed = tasks.Count(t => t.StatusId == 4);
                var forReview = tasks.Count(t => t.StatusId == 3);
                var inProgress = tasks.Count(t => t.StatusId == 2);
                var notStarted = tasks.Count(t => t.StatusId == 1);

                return Ok(new
                {
                    ProjectId = projectId,
                    ProjectName = project.Name,
                    TotalTasks = total,
                    CompletionPercentage = total == 0 ? 0.0 : Math.Round((double)completed / total * 100, 2), 
                    Completed = new
                    {
                        Count = completed,
                        Percentage = total == 0 ? 0.0 : Math.Round((double)completed / total * 100, 2)
                    },
                    ForReview = new
                    {
                        Count = forReview,
                        Percentage = total == 0 ? 0.0 : Math.Round((double)forReview / total * 100, 2)
                    },
                    InProgress = new
                    {
                        Count = inProgress,
                        Percentage = total == 0 ? 0.0 : Math.Round((double)inProgress / total * 100, 2)
                    },
                    NotStarted = new
                    {
                        Count = notStarted,
                        Percentage = total == 0 ? 0.0 : Math.Round((double)notStarted / total * 100, 2)
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}