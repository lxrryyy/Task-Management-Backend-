using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Data;
using TaskManagement.DTOs.Task;
using TaskManagement.Models;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly AccountDbContext _context;

        public TaskController(AccountDbContext context)
        {
            _context = context;
        }


        [HttpGet("GetAllTasksPriorities")]
        public async Task<IActionResult> GetAllTasksPriorities()
        {
            try
            {
                var priorities = await _context.TaskPriorities
                    .Where(t => t.IsActive)
                    .Select(t => new
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Description = t.Description,
                        Active = t.IsActive,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync();

                return Ok(priorities);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet("GetAllTasksStatuses")]
        public async Task<IActionResult> GetAllTasksStatuses()
        {
            try
            {
                var statuses = await _context.TaskStatuses
                    .Where(t => t.IsActive)
                    .Select(t => new
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Description = t.Description,
                        Active = t.IsActive,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync();

                return Ok(statuses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        // GET all tasks
        [HttpGet("GetAllTasks")]
        public async Task<IActionResult> GetAllTasks()
        {
            try
            {
                var tasks = await _context.Tasks
                    .Where(t => !t.IsDeleted)
                    .Select(t => new TaskResponseDTO
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description,
                        StatusId = t.StatusId,           
                        StatusName = t.Status.Name,      
                        PriorityId = t.PriorityId,       
                        PriorityName = t.Priority.Name,  
                        ParentTaskId = t.ParentTaskId,
                        CreatorId = t.CreatorId,
                        CreatorName = t.Creator.Name,
                        StoryPoints = t.StoryPoints,
                        ProjectId = t.ProjectId,
                        StartDate = t.StartDate,
                        DueDate = t.DueDate,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt,
                        AssigneeIds = t.Assignments.Select(a => a.AccountId).ToList()
                    })
                    .ToListAsync();

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET task by id
        [HttpGet("GetTaskById/{id}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            try
            {
                var task = await _context.Tasks
                    .Where(t => t.Id == id && !t.IsDeleted)
                    .Select(t => new TaskResponseDTO
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description,
                        StatusId = t.StatusId,           
                        StatusName = t.Status.Name,      
                        PriorityId = t.PriorityId,       
                        PriorityName = t.Priority.Name,  
                        CreatorId = t.CreatorId,
                        ProjectId = t.ProjectId,
                        ParentTaskId = t.ParentTaskId,
                        CreatorName = t.Creator.Name,
                        StoryPoints = t.StoryPoints,
                        StartDate = t.StartDate,
                        DueDate = t.DueDate,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt,
                        AssigneeIds = t.Assignments.Select(a => a.AccountId).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (task == null)
                    return NotFound("Task not found.");

                return Ok(task);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST create task
        [HttpPost("CreateTask")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDTO dto, [FromQuery] int creatorId)
        {
            try
            {
                var creator = await _context.Accounts.FindAsync(creatorId);
                if (creator == null)
                    return NotFound("Creator account not found.");

                if (creator.Role != "Admin")
                {
                    var projectMember = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == dto.ProjectId && m.AccountId == creatorId);

                    if (projectMember == null)
                        return StatusCode(403, "You are not a member of this project.");

                    var allowedRoles = new[] { "ProjectManager", "ScrumMaster", "ProjectManager-ScrumMaster" };
                    if (!allowedRoles.Contains(projectMember.Role))
                        return StatusCode(403, "Only Admin, Project Manager, or Scrum Master can create tasks.");
                }

                // Validate story points
                if (dto.StoryPoints.HasValue && (dto.StoryPoints < 1 || dto.StoryPoints > 5))
                    return BadRequest("Story points must be between 1 and 5.");

                // Validate duplicate assigneeIds
                if (dto.AssigneeIds.Distinct().Count() != dto.AssigneeIds.Count)
                    return BadRequest("Duplicate assignee IDs are not allowed.");

                // Validate PriorityId if provided
                if (dto.PriorityId.HasValue)
                {
                    var priorityExists = await _context.TaskPriorities.AnyAsync(p => p.Id == dto.PriorityId.Value);
                    if (!priorityExists)
                        return BadRequest("Invalid PriorityId.");
                }

                var task = new TaskItem
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    PriorityId = dto.PriorityId, 
                    StatusId = 1,                  
                    StartDate = dto.StartDate,
                    DueDate = dto.DueDate,
                    StoryPoints = dto.StoryPoints,
                    ProjectId = dto.ProjectId,
                    ParentTaskId = dto.ParentTaskId,
                    CreatorId = creatorId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                // Auto set project status to Active (StatusId = 2)
                var project = await _context.Projects.FindAsync(dto.ProjectId);
                if (project != null && project.StatusId == 1) // 1 = Not Started
                {
                    project.StatusId = 2; // 2 = Active
                    project.UpdatedAt = DateTime.UtcNow;

                    _context.TimeLogs.Add(new TimeLog
                    {
                        TaskId = null,
                        AccountId = creatorId,
                        Action = "ProjectStatusChanged",
                        OldValue = "Not Started",
                        NewValue = "Active",
                        Note = "Project set to Active because a task was created"
                    });
                }

                // Assign users
                if (dto.AssigneeIds.Any())
                {
                    foreach (var accountId in dto.AssigneeIds)
                    {
                        _context.TaskAssignments.Add(new TaskAssignment
                        {
                            TaskId = task.Id,
                            AccountId = accountId,
                            AssignedById = creatorId,
                            AssignedAt = DateTime.UtcNow
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // Time log
                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = task.Id,
                    AccountId = creatorId,
                    Action = "Created",
                    NewValue = task.Title,
                    Note = dto.ParentTaskId == null
                        ? $"Task created by {creator.Name} ({creator.Role})"
                        : $"Subtask created by {creator.Name} ({creator.Role})"
                });

                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetTaskById), new { id = task.Id }, task);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PATCH update task
        [HttpPatch("UpdateTask/{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskDTO dto, [FromQuery] int updaterId)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(id);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var updater = await _context.Accounts.FindAsync(updaterId);
                if (updater == null)
                    return NotFound("Updater account not found.");

                if (updater.Role != "Admin")
                {
                    var projectMember = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == updaterId);

                    if (projectMember == null)
                        return StatusCode(403, "You are not a member of this project.");

                    var allowedRoles = new[] { "ProjectManager", "ScrumMaster", "ProjectManager-ScrumMaster" };
                    if (!allowedRoles.Contains(projectMember.Role))
                        return StatusCode(403, "Only Admin, Project Manager, or Scrum Master can update tasks.");
                }

                // Validate story points
                if (dto.StoryPoints.HasValue && (dto.StoryPoints < 1 || dto.StoryPoints > 5))
                    return BadRequest("Story points must be between 1 and 5.");

                var changes = new List<string>();

                if (dto.Title != null && dto.Title != task.Title)
                {
                    changes.Add($"Title: {task.Title} → {dto.Title}");
                    task.Title = dto.Title;
                }
                if (dto.Description != null && dto.Description != task.Description)
                {
                    changes.Add($"Description updated");
                    task.Description = dto.Description;
                }

                if (dto.StatusId.HasValue && dto.StatusId != task.StatusId)
                {
                    var statusExists = await _context.TaskStatuses.AnyAsync(s => s.Id == dto.StatusId.Value);
                    if (!statusExists)
                        return BadRequest("Invalid StatusId.");

                    changes.Add($"StatusId: {task.StatusId} → {dto.StatusId}");
                    task.StatusId = dto.StatusId.Value;
                }

              
                if (dto.PriorityId.HasValue && dto.PriorityId != task.PriorityId)
                {
                    var priorityExists = await _context.TaskPriorities.AnyAsync(p => p.Id == dto.PriorityId.Value);
                    if (!priorityExists)
                        return BadRequest("Invalid PriorityId.");

                    changes.Add($"PriorityId: {task.PriorityId} → {dto.PriorityId}");
                    task.PriorityId = dto.PriorityId;
                }

                if (dto.StartDate != null && dto.StartDate != task.StartDate)
                {
                    changes.Add($"StartDate: {task.StartDate} → {dto.StartDate}");
                    task.StartDate = dto.StartDate;
                }
                if (dto.DueDate != null && dto.DueDate != task.DueDate)
                {
                    changes.Add($"DueDate: {task.DueDate} → {dto.DueDate}");
                    task.DueDate = dto.DueDate;
                }
                if (dto.StoryPoints.HasValue && dto.StoryPoints != task.StoryPoints)
                {
                    changes.Add($"StoryPoints: {task.StoryPoints} → {dto.StoryPoints}");
                    task.StoryPoints = dto.StoryPoints;
                }
                if (dto.ParentTaskId != task.ParentTaskId)
                {
                    changes.Add($"ParentTaskId: {task.ParentTaskId} → {dto.ParentTaskId}");
                    task.ParentTaskId = dto.ParentTaskId;
                }

                task.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                if (changes.Any())
                {
                    _context.TimeLogs.Add(new TimeLog
                    {
                        TaskId = task.Id,
                        AccountId = updaterId,
                        Action = "Updated",
                        NewValue = string.Join(", ", changes),
                        Note = "Task updated"
                    });
                    await _context.SaveChangesAsync();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PATCH update task status (for assigned members)
        [HttpPatch("UpdateTaskStatus/{id}")]
        public async Task<IActionResult> UpdateTaskStatus(int id, [FromQuery] int requesterId, [FromBody] UpdateTaskStatusDTO dto)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(id);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var isAssigned = await _context.TaskAssignments
                    .AnyAsync(a => a.TaskId == id && a.AccountId == requesterId);

                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                if (requester.Role != "Admin")
                {
                    var projectMember = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == requesterId);

                    var isPrivileged = projectMember?.Role == "ProjectManager" ||
                                       projectMember?.Role == "ScrumMaster" ||
                                       projectMember?.Role == "ProjectManager-ScrumMaster";

                    if (!isPrivileged && !isAssigned)
                        return StatusCode(403, "You are not assigned to this task.");
                }

                // Validate StatusId
                var statusExists = await _context.TaskStatuses.AnyAsync(s => s.Id == dto.StatusId);
                if (!statusExists)
                    return BadRequest("Invalid StatusId.");

                var oldStatusId = task.StatusId;
                task.StatusId = dto.StatusId; 
                task.UpdatedAt = DateTime.UtcNow;

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = task.Id,
                    AccountId = requesterId,
                    Action = "StatusUpdated",
                    OldValue = oldStatusId.ToString(),
                    NewValue = dto.StatusId.ToString(),
                    Note = $"Status changed from {oldStatusId} to {dto.StatusId}"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // DELETE task (soft delete)
        [HttpDelete("DeleteTask/{id}")]
        public async Task<IActionResult> DeleteTask(int id, [FromQuery] int deleterId)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(id);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                task.IsDeleted = true;
                task.UpdatedAt = DateTime.UtcNow;

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = task.Id,
                    AccountId = deleterId,
                    Action = "Deleted",
                    OldValue = task.Title,
                    Note = "Task deleted"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PATCH assign task
        [HttpPatch("AssignTask/{id}")]
        public async Task<IActionResult> AssignTask(int id, [FromBody] AssignTaskDTO dto)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(id);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var assigner = await _context.Accounts.FindAsync(dto.AssignedById);
                if (assigner == null)
                    return NotFound("Assigner account not found.");

                if (assigner.Role != "Admin")
                {
                    var projectMember = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == dto.AssignedById);

                    if (projectMember == null)
                        return StatusCode(403, "You are not a member of this project.");

                    var allowedRoles = new[] { "ProjectManager", "ScrumMaster", "ProjectManager-ScrumMaster" };
                    if (!allowedRoles.Contains(projectMember.Role))
                        return StatusCode(403, "Only Admin, Project Manager, or Scrum Master can assign tasks.");
                }

                var existing = _context.TaskAssignments.Where(a => a.TaskId == id);
                _context.TaskAssignments.RemoveRange(existing);

                foreach (var accountId in dto.AssigneeIds)
                {
                    _context.TaskAssignments.Add(new TaskAssignment
                    {
                        TaskId = id,
                        AccountId = accountId,
                        AssignedById = dto.AssignedById,
                        AssignedAt = DateTime.UtcNow
                    });
                }

                task.UpdatedAt = DateTime.UtcNow;

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = id,
                    AccountId = dto.AssignedById,
                    Action = "Assigned",
                    NewValue = string.Join(", ", dto.AssigneeIds),
                    Note = "Task assigned"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET tasks by assignee
        [HttpGet("GetTasksByAssignee/{accountId}")]
        public async Task<IActionResult> GetTasksByAssignee(int accountId)
        {
            try
            {
                var tasks = await _context.Tasks
                    .Where(t => !t.IsDeleted && t.Assignments.Any(a => a.AccountId == accountId))
                    .Select(t => new TaskResponseDTO
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description,
                        StatusId = t.StatusId,           
                        StatusName = t.Status.Name,      
                        PriorityId = t.PriorityId,       
                        PriorityName = t.Priority.Name,  
                        CreatorId = t.CreatorId,
                        CreatorName = t.Creator.Name,
                        StoryPoints = t.StoryPoints,
                        ProjectId = t.ProjectId,
                        ParentTaskId = t.ParentTaskId,
                        StartDate = t.StartDate,
                        DueDate = t.DueDate,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt,
                        AssigneeIds = t.Assignments.Select(a => a.AccountId).ToList()
                    })
                    .ToListAsync();

                if (!tasks.Any())
                    return NotFound("No tasks found for this account.");

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET tasks by project with role-based visibility
        [HttpGet("GetTasksByProject/{projectId}")]
        public async Task<IActionResult> GetTasksByProject(int projectId, [FromQuery] int requesterId)
        {
            try
            {
                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                IQueryable<TaskItem> query = _context.Tasks
                    .Where(t => t.ProjectId == projectId && !t.IsDeleted);

                if (requester.Role != "Admin")
                {
                    var projectMember = await _context.ProjectMembers
                        .SingleOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == requesterId);

                    if (projectMember == null)
                        return StatusCode(403, "You are not a member of this project.");

                    var isPrivileged = projectMember.Role == "ProjectManager" ||
                                       projectMember.Role == "ScrumMaster" ||
                                       projectMember.Role == "ProjectManager-ScrumMaster";

                    if (!isPrivileged)
                        query = query.Where(t => t.Assignments.Any(a => a.AccountId == requesterId));
                }

                var tasks = await query
                    .Select(t => new TaskResponseDTO
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Description = t.Description,
                        StatusId = t.StatusId,           
                        StatusName = t.Status.Name,      
                        PriorityId = t.PriorityId,       
                        PriorityName = t.Priority.Name,  
                        CreatorId = t.CreatorId,
                        CreatorName = t.Creator.Name,
                        StoryPoints = t.StoryPoints,
                        ProjectId = t.ProjectId,
                        ParentTaskId = t.ParentTaskId,
                        StartDate = t.StartDate,
                        DueDate = t.DueDate,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt,
                        AssigneeIds = t.Assignments.Select(a => a.AccountId).ToList()
                    })
                    .ToListAsync();

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}