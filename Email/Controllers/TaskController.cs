using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.Pkcs;
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
                        AssigneeIds = t.Assignments.Where(a => !a.IsDeleted).Select(a => a.AccountId).ToList()
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
                        AssigneeIds = t.Assignments.Where(a => !a.IsDeleted).Select(a => a.AccountId).ToList()
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
                        .FirstOrDefaultAsync(m => m.ProjectId == dto.ProjectId && m.AccountId == creatorId && !m.IsDeleted);

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
                        Note = $"Project set to Active because a task was created by {creator.Name} ({creator.Role})"
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
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    AccountId = creatorId,
                    Action = "Created",
                    NewValue = task.Title,
                    Note = dto.ParentTaskId == null
                        ? $"Task created by {creator.Name} ({creator.Role})"
                        : $"Subtask created by {creator.Name} ({creator.Role})"
                });

                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetTaskById), new { id = task.Id }, new
                {
                    id = task.Id,
                    title = task.Title,
                    description = task.Description,
                    statusId = task.StatusId,
                    priorityId = task.PriorityId,
                    projectId = task.ProjectId,
                    parentTaskId = task.ParentTaskId,
                    creatorId = task.CreatorId,
                    storyPoints = task.StoryPoints,
                    startDate = task.StartDate,
                    dueDate = task.DueDate,
                    createdAt = task.CreatedAt,
                    updatedAt = task.UpdatedAt
                });
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

                var updaterProjectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == updaterId && !m.IsDeleted);
                var updaterProjectRole = updater.Role == "Admin" ? "Admin" : updaterProjectMember?.Role ?? "Unknown";

                await _context.SaveChangesAsync();

                if (changes.Any())
                {
                    _context.TimeLogs.Add(new TimeLog
                    {
                        ProjectId = task.ProjectId,
                        TaskId = task.Id,
                        AccountId = updaterId,
                        Action = "Updated",
                        NewValue = string.Join(", ", changes),
                        Note = $"Task updated by {updater.Name}, ({updaterProjectRole})"
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
                    .AnyAsync(a => a.TaskId == id && a.AccountId == requesterId && !a.IsDeleted);

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

                var oldStatus = await _context.TaskStatuses.FirstOrDefaultAsync(s => s.Id == task.StatusId);
                var newStatus = await _context.TaskStatuses.FirstOrDefaultAsync(s => s.Id == dto.StatusId);
                if (newStatus == null)
                    return BadRequest("Invalid StatusId.");

                var oldStatusName = oldStatus?.Name ?? task.StatusId.ToString();
                var newStatusName = newStatus.Name;

                task.StatusId = dto.StatusId;
                task.UpdatedAt = DateTime.UtcNow;

                _context.TimeLogs.Add(new TimeLog
                {
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    AccountId = requesterId,
                    Action = "Status Updated",
                    OldValue = oldStatusName,
                    NewValue = newStatusName,
                    Note = $"Status changed from {oldStatusName} to {newStatusName}"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("DeleteTask/{id}")]
        public async Task<IActionResult> DeleteTask(int id, [FromQuery] int deleterId)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(id);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var deleter = await _context.Accounts.FindAsync(deleterId);
                if (deleter == null)
                    return NotFound("Deleter account not found.");

                var deleterProjectMember = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == deleterId && !m.IsDeleted);

                var deleterRole = deleter.Role == "Admin" ? "Admin" : deleterProjectMember?.Role ?? "Unknown";

                await SoftDeleteTaskRecursive(id);

                _context.TimeLogs.Add(new TimeLog
                {
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    AccountId = deleterId,
                    Action = "Deleted",
                    OldValue = task.Title,
                    Note = $"Task and all subtasks deleted by {deleter.Name}, ({deleterRole})"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task SoftDeleteTaskRecursive(int taskId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null || task.IsDeleted) return;

            task.IsDeleted = true;
            task.UpdatedAt = DateTime.UtcNow;

            var assignments = await _context.TaskAssignments
                .Where(a => a.TaskId == taskId && !a.IsDeleted)
                .ToListAsync();

            foreach (var assignment in assignments)
            {
                assignment.IsDeleted = true;
                assignment.DeletedAt = DateTime.UtcNow;
            }

            var subtasks = await _context.Tasks
                .Where(t => t.ParentTaskId == taskId && !t.IsDeleted)
                .ToListAsync();

            foreach (var subtask in subtasks)
                await SoftDeleteTaskRecursive(subtask.Id);
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
                        .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == dto.AssignedById && !m.IsDeleted);

                    if (projectMember == null)
                        return StatusCode(403, "You are not a member of this project.");

                    var allowedRoles = new[] { "ProjectManager", "ScrumMaster", "ProjectManager-ScrumMaster" };
                    if (!allowedRoles.Contains(projectMember.Role))
                        return StatusCode(403, "Only Admin, Project Manager, or Scrum Master can assign tasks.");
                }

                var existing = await _context.TaskAssignments
                    .Where(a => a.TaskId == id && !a.IsDeleted)
                    .ToListAsync();

                foreach (var a in existing)
                {
                    a.IsDeleted = true;
                    a.DeletedAt = DateTime.UtcNow;
                }

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
                var assignerProjectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == dto.AssignedById && !m.IsDeleted);

                var assignerProjectRole = assigner.Role == "Admin" ? "Admin" : assignerProjectMember?.Role ?? "Unknown";

                _context.TimeLogs.Add(new TimeLog
                {
                    ProjectId = task.ProjectId,
                    TaskId = id,
                    AccountId = dto.AssignedById,
                    Action = "Assigned",
                    NewValue = string.Join(", ", dto.AssigneeIds),
                    Note = $"Task assigned by {assigner.Name}, {assignerProjectRole}"
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
                    .Where(t => !t.IsDeleted && t.Assignments.Any(a => a.AccountId == accountId && !a.IsDeleted))
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
                        AssigneeIds = t.Assignments.Where(a => !a.IsDeleted).Select(a => a.AccountId).ToList()
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
                        query = query.Where(t => t.Assignments.Any(a => a.AccountId == requesterId && !a.IsDeleted));
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
                        AssigneeIds = t.Assignments.Where(a => !a.IsDeleted).Select(a => a.AccountId).ToList()
                    })
                    .ToListAsync();

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        //deleted tasks by project
        [HttpGet("GetDeletedTasksByProject/{projectId}")]
        public async Task<IActionResult> GetDeletedTasksByProject(int projectId)
        {
            try
            {
                var tasks = await _context.Tasks
                    .Where(t => t.ProjectId == projectId && t.IsDeleted && t.ParentTaskId == null)
                    .Select(t => new
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
                        ProjectId = t.ProjectId,
                        StoryPoints = t.StoryPoints,
                        StartDate = t.StartDate,
                        DueDate = t.DueDate,
                        UpdatedAt = t.UpdatedAt

                    })
                    .ToListAsync();

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET deleted subtasks by parent task
        [HttpGet("GetDeletedSubtasks/{parentTaskId}")]
        public async Task<IActionResult> GetDeletedSubtasks(int parentTaskId)
        {
            try
            {
                var subtasks = await _context.Tasks
                    .Where(t => t.ParentTaskId == parentTaskId && t.IsDeleted)
                    .Select(t => new
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
                        ProjectId = t.ProjectId,
                        ParentTaskId = t.ParentTaskId,
                        StoryPoints = t.StoryPoints,
                        StartDate = t.StartDate,
                        DueDate = t.DueDate,
                        UpdatedAt = t.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(subtasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PATCH reactivate task and  subtasks
        [HttpPatch("ReactivateTask/{taskId}")]
        public async Task<IActionResult> ReactivateTask(int taskId, [FromQuery] int requesterId)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(taskId);
                if (task == null || !task.IsDeleted)
                    return NotFound("Deleted task not found.");

                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                var requesterProjectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == requesterId && !m.IsDeleted);
                var requesterRole = requester.Role == "Admin" ? "Admin" : requesterProjectMember?.Role ?? "Unknown";

                var restoredCount = await ReactivateTaskRecursive(taskId);

                _context.TimeLogs.Add(new TimeLog
                {  
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    AccountId = requesterId,
                    Action = "TaskReactivated",
                    NewValue = task.Title,
                    Note = $"Task and all subtasks reactivated {requester.Name}, {requesterRole}"
                });

                await _context.SaveChangesAsync();
                return Ok(new
                {
                    message = "Task reactivated successfully.",
                    taskId = task.Id,
                    restoredSubtasks = restoredCount - 1
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<int> ReactivateTaskRecursive(int taskId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task == null) return 0;

            task.IsDeleted = false;
            task.UpdatedAt = DateTime.UtcNow;

            var assignments = await _context.TaskAssignments
                .Where(a => a.TaskId == taskId && a.IsDeleted)
                .ToListAsync();
            foreach (var assignment in assignments)
            {
                assignment.IsDeleted = false;
                assignment.DeletedAt = null;
            }

            var subtasks = await _context.Tasks
                .Where(t => t.ParentTaskId == taskId && t.IsDeleted)
                .ToListAsync();

            int count = 1;
            foreach (var subtask in subtasks)
                count += await ReactivateTaskRecursive(subtask.Id);

            return count;
        }
    }
}