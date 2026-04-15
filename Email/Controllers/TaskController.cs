using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using TaskManagement.Data;
using TaskManagement.DTOs.Task;
using TaskManagement.Helpers;
using TaskManagement.Models;
using TaskManagement.Services;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly AccountDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly IEmailService _emailService;
        private static DateTime PhTime =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));
        public TaskController(AccountDbContext context, NotificationService notificationService, IEmailService emailService)
        {
            _context = context;
            _notificationService = notificationService;
            _emailService = emailService;
        }


        private async Task<List<object>> CheckWorkloadWarnings(int taskId, List<int> assigneeIds)
        {
            var warnings = new List<object>();
            var assigneeIdSet = assigneeIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (assigneeIdSet.Count == 0)
                return warnings;

            var task = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new { t.Id, t.StartDate, t.DueDate, t.StoryPoints })
                .FirstOrDefaultAsync();
            if (task == null || !task.StoryPoints.HasValue)
                return warnings;
            if (!task.StartDate.HasValue || !task.DueDate.HasValue)
                return warnings;

            var accountNames = await _context.Accounts
                .AsNoTracking()
                .Where(a => assigneeIdSet.Contains(a.Id))
                .Select(a => new { a.Id, a.Name })
                .ToDictionaryAsync(a => a.Id, a => a.Name);

            var overlappingStoryPoints = await _context.TaskAssignments
                .AsNoTracking()
                .Where(a =>
                    assigneeIdSet.Contains(a.AccountId) &&
                    !a.IsDeleted &&
                    !a.Task.IsDeleted &&
                    a.TaskId != taskId &&
                    a.Task.StoryPoints != null &&
                    a.Task.StartDate.HasValue &&
                    a.Task.DueDate.HasValue &&
                    a.Task.StartDate.Value.Date <= task.DueDate.Value.Date &&
                    a.Task.DueDate.Value.Date >= task.StartDate.Value.Date)
                .Select(a => new { a.AccountId, StoryPoints = a.Task.StoryPoints!.Value })
                .ToListAsync();

            var existingHoursByAccount = overlappingStoryPoints
                .GroupBy(x => x.AccountId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => BusinessDayHelper.GetHoursForStoryPoints(x.StoryPoints))
                );

            var newTaskHours = BusinessDayHelper.GetHoursForStoryPoints(task.StoryPoints.Value);
            foreach (var accountId in assigneeIdSet)
            {
                var existingHours = existingHoursByAccount.TryGetValue(accountId, out var val) ? val : 0;
                var totalHours = existingHours + newTaskHours;
                if (totalHours <= 8)
                    continue;

                var accountName = accountNames.TryGetValue(accountId, out var n) ? n : null;
                warnings.Add(new
                {
                    accountId,
                    accountName,
                    totalHours,
                    newTaskHours,
                    existingHours,
                    capacity = 8,
                    overloadBy = totalHours - 8,
                    message = $"{accountName} is overloaded by {totalHours - 8}h " +
                              $"({totalHours}h total / 8h daily capacity) " +
                              $"during {task.StartDate:yyyy-MM-dd} to {task.DueDate:yyyy-MM-dd}."
                });
            }

            return warnings;
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

        [HttpGet("CheckAssigneeWorkload")]
        public async Task<IActionResult> CheckAssigneeWorkload(
            [FromQuery] DateTime startDate,
            [FromQuery] int storyPoints,
            [FromQuery] List<int> assigneeIds,
            [FromQuery] int projectId)
        {
            var validStoryPoints = new[] { 1, 2, 3, 5, 8, 13, 21 };
            if (!validStoryPoints.Contains(storyPoints))
                return BadRequest("Story points must be a Fibonacci number (1, 2, 3, 5, 8, 13, 21).");

            if (startDate == default)
                return BadRequest("Start date is required.");

            if (!assigneeIds.Any())
                return Ok(new { warnings = Array.Empty<object>() });

            var projectedDueDate = BusinessDayHelper.CalculateDueDateFromStoryPoints(startDate, storyPoints);
            var newTaskHours = BusinessDayHelper.GetHoursForStoryPoints(storyPoints);

            var warnings = new List<object>();

            var assigneeIdSet = assigneeIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (assigneeIdSet.Count == 0)
                return Ok(new
                {
                    projectedStartDate = startDate.ToString("yyyy-MM-dd HH:mm"),
                    projectedDueDate = projectedDueDate.ToString("yyyy-MM-dd HH:mm"),
                    storyPoints,
                    warnings
                });

            var accountNames = await _context.Accounts
                .AsNoTracking()
                .Where(a => assigneeIdSet.Contains(a.Id))
                .Select(a => new { a.Id, a.Name })
                .ToDictionaryAsync(a => a.Id, a => a.Name);

            var overlappingRows = await _context.TaskAssignments
                .AsNoTracking()
                .Where(a =>
                    assigneeIdSet.Contains(a.AccountId) &&
                    !a.IsDeleted &&
                    !a.Task.IsDeleted &&
                    a.Task.StoryPoints != null &&
                    a.Task.StartDate.HasValue &&
                    a.Task.DueDate.HasValue &&
                    a.Task.StartDate.Value.Date <= projectedDueDate.Date &&
                    a.Task.DueDate.Value.Date >= startDate.Date)
                .Select(a => new { a.AccountId, StoryPoints = a.Task.StoryPoints!.Value })
                .ToListAsync();

            var existingHoursByAccount = overlappingRows
                .GroupBy(x => x.AccountId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => BusinessDayHelper.GetHoursForStoryPoints(x.StoryPoints))
                );

            foreach (var accountId in assigneeIdSet)
            {
                var existingHours = existingHoursByAccount.TryGetValue(accountId, out var val) ? val : 0;
                var totalHours = existingHours + newTaskHours;
                if (totalHours <= 8)
                    continue;

                var accountName = accountNames.TryGetValue(accountId, out var n) ? n : null;
                warnings.Add(new
                {
                    accountId,
                    accountName,
                    existingHours,
                    newTaskHours,
                    totalHours,
                    capacity = 8,
                    overloadBy = totalHours - 8,
                    projectedStartDate = startDate.ToString("yyyy-MM-dd HH:mm"),
                    projectedDueDate = projectedDueDate.ToString("yyyy-MM-dd HH:mm"),
                    message = $"{accountName} is overloaded by {totalHours - 8}h " +
                              $"({totalHours}h total / 8h daily capacity) " +
                              $"during {startDate:yyyy-MM-dd} to {projectedDueDate:yyyy-MM-dd}."
                });
            }

            return Ok(new
            {
                projectedStartDate = startDate.ToString("yyyy-MM-dd HH:mm"),
                projectedDueDate = projectedDueDate.ToString("yyyy-MM-dd HH:mm"),
                storyPoints,
                warnings
            });
        }

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

                    var allowedRoles = new[] { "Project Manager", "Scrum Master", "Project Manager - Scrum Master" };
                    if (!allowedRoles.Contains(projectMember.Role))
                        return StatusCode(403, "Only Admin, Project Manager, or Scrum Master can create tasks.");
                }

                if (dto.StartDate == default)
                    return BadRequest("Start date is required.");

                if (!dto.StoryPoints.HasValue)
                    return BadRequest("Story points are required to calculate the due date.");

                var validStoryPoints = new[] { 1, 2, 3, 5, 8, 13, 21 };
                if (!validStoryPoints.Contains(dto.StoryPoints.Value))
                    return BadRequest("Story points must be a Fibonacci number (1, 2, 3, 5, 8, 13, 21).");

                // Auto-calculate DueDate 
                var calculatedDueDate = BusinessDayHelper.CalculateDueDateFromStoryPoints(
                    dto.StartDate,
                    dto.StoryPoints.Value
                );

                var project = await _context.Projects.FindAsync(dto.ProjectId);
                if (project == null)
                    return NotFound("Project not found.");
                if (calculatedDueDate > project.EndDate)
                    return BadRequest(
                        $"Calculated due date ({calculatedDueDate:yyyy-MM-dd}) exceeds " +
                        $"the project end date ({project.EndDate:yyyy-MM-dd}).");

                if (dto.AssigneeIds.Distinct().Count() != dto.AssigneeIds.Count)
                    return BadRequest("Duplicate assignee IDs are not allowed.");

                if (dto.AssigneeIds.Any())
                {
                    var validAccountIds = await _context.Accounts
                        .Where(a => dto.AssigneeIds.Contains(a.Id))
                        .Select(a => a.Id)
                        .ToListAsync();

                    var invalidIds = dto.AssigneeIds.Except(validAccountIds).ToList();
                    if (invalidIds.Any())
                        return BadRequest($"The following assignee IDs do not exist: {string.Join(", ", invalidIds)}");
                }

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
                    DueDate = calculatedDueDate,   //calculated result
                    StoryPoints = dto.StoryPoints,
                    ProjectId = dto.ProjectId,
                    ParentTaskId = dto.ParentTaskId,
                    CreatorId = creatorId,
                    CreatedAt = PhTime,
                    UpdatedAt = PhTime
                };

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                var projectMemberCheck = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == dto.ProjectId && m.AccountId == creatorId && !m.IsDeleted);
                var creatorRole = creator.Role == "Admin" ? "Admin" : projectMemberCheck?.Role;

                if (project.StatusId == 1)
                {
                    project.StatusId = 2;
                    project.UpdatedAt = PhTime;

                    _context.AuditLogs.Add(new AuditLog
                    {
                        ProjectId = project.Id,
                        TaskId = null,
                        AccountId = creatorId,
                        Action = "UPDATED",
                        OldValue = "Not Started",
                        NewValue = "Active",
                        Note = $"Project '{project.Name}' set to Active because a task was created by {creator.Name} ({creatorRole}).",
						CreatedAt = PhTime
					});
                }

                if (dto.AssigneeIds.Any())
                {
                    foreach (var accountId in dto.AssigneeIds)
                    {
                        _context.TaskAssignments.Add(new TaskAssignment
                        {
                            TaskId = task.Id,
                            AccountId = accountId,
                            AssignedById = creatorId,
                            AssignedAt = PhTime
                        });

                        await _notificationService.NotifyAsync(
                           accountId,
                           $"You have been assigned to task: '{task.Title}' in project '{project.Name}'.",
                           projectId: task.ProjectId,
                           taskId: task.Id
                        );
                        var assigneeAccount = await _context.Accounts.FindAsync(accountId);
                        if (assigneeAccount?.Email != null)
                            await _emailService.SendTaskAssignedAsync(assigneeAccount.Email, task.Title);
                    }
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    AccountId = creatorId,
                    Action = "CREATED",
                    NewValue = task.Title,
                    Note = dto.ParentTaskId == null
                        ? $"Task created '{task.Title}' by {creator.Name} ({creatorRole})."
                        : $"Subtask created'{task.Title}' by {creator.Name} ({creatorRole}).",

					CreatedAt = PhTime
				});

                await _context.SaveChangesAsync();
                var warnings = await CheckWorkloadWarnings(task.Id, dto.AssigneeIds);
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
                    dueDate = task.DueDate,        //calculated value returned
                    createdAt = task.CreatedAt,
                    updatedAt = task.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }


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

                    var allowedRoles = new[] { "Project Manager", "Scrum Master", "Project Manager - Scrum Master" };
                    if (!allowedRoles.Contains(projectMember.Role))
                        return StatusCode(403, "Only Admin, Project Manager, or Scrum Master can update tasks.");
                }

                if (dto.StartDate == default)
                    return BadRequest("Start date is required.");

                var validStoryPoints = new[] { 1, 2, 3, 5, 8, 13, 21 };
                if (dto.StoryPoints.HasValue && !validStoryPoints.Contains(dto.StoryPoints.Value))
                    return BadRequest("Story points must be a Fibonacci number (1, 2, 3, 5, 8, 13, 21).");

                // recalculate
                var effectiveStoryPoints = dto.StoryPoints ?? task.StoryPoints;
                if (!effectiveStoryPoints.HasValue)
                    return BadRequest("Story points are required to recalculate the due date.");

                var recalculatedDueDate = BusinessDayHelper.CalculateDueDateFromStoryPoints(
                    dto.StartDate,
                    effectiveStoryPoints.Value
                );

                var taskProject = await _context.Projects.FindAsync(task.ProjectId);
                if (taskProject == null)
                    return NotFound("Project not found.");
                if (recalculatedDueDate > taskProject.EndDate)
                    return BadRequest(
                        $"Recalculated due date ({recalculatedDueDate:yyyy-MM-dd}) exceeds " +
                        $"the project end date ({taskProject.EndDate:yyyy-MM-dd}).");

                var changes = new List<string>();

                if (dto.Title != null && dto.Title != task.Title)
                {
                    changes.Add($"Title: {task.Title} → {dto.Title}");
                    task.Title = dto.Title;
                }
                if (dto.Description != null && dto.Description != task.Description)
                {
                    changes.Add("Description updated");
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
                if (dto.StartDate != task.StartDate)
                {
                    changes.Add($"StartDate: {task.StartDate} → {dto.StartDate}");
                    task.StartDate = dto.StartDate;
                }
                if (recalculatedDueDate != task.DueDate)
                {
                    changes.Add($"DueDate recalculated: {task.DueDate:yyyy-MM-dd HH:mm} → {recalculatedDueDate:yyyy-MM-dd HH:mm}");
                    task.DueDate = recalculatedDueDate;  // ← always the calculated value
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

                task.UpdatedAt = PhTime;

                var updaterProjectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == updaterId && !m.IsDeleted);
                var updaterProjectRole = updater.Role == "Admin" ? "Admin" : updaterProjectMember?.Role;

                if (changes.Any())
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        ProjectId = task.ProjectId,
                        TaskId = task.Id,
                        AccountId = updaterId,
                        Action = "UPDATED",
                        NewValue = string.Join(", ", changes),
                        Note = $"Task updated '{task.Title}' by {updater.Name} ({updaterProjectRole}).",
						CreatedAt = PhTime
					});
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPatch("UpdateTaskStatus/{id}")]
        public async Task<IActionResult> UpdateTaskStatus(int id, [FromQuery] int requesterId, [FromBody] UpdateTaskStatusDTO dto)
        {
            try
            {
                var task = await _context.Tasks.FindAsync(id);
                if (task == null || task.IsDeleted)
                    return NotFound("Task not found.");

                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                var isAssigned = await _context.TaskAssignments
                    .AnyAsync(a => a.TaskId == id && a.AccountId == requesterId && !a.IsDeleted);

                var projectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == requesterId && !m.IsDeleted);

                var isAdmin = requester.Role == "Admin";
                var isProjectManager = projectMember?.Role == "Project Manager" ||
                                       projectMember?.Role == "Project Manager - Scrum Master";
                var isPrivileged = isProjectManager || projectMember?.Role == "Scrum Master";

                if (!isAdmin && !isPrivileged && !isAssigned)
                    return StatusCode(403, "You are not assigned to this task.");

                var newStatus = await _context.TaskStatuses.FirstOrDefaultAsync(s => s.Id == dto.StatusId);
                if (newStatus == null)
                    return BadRequest("Invalid StatusId.");

                if (dto.StatusId == 4)
                {
                    if (task.ParentTaskId == null && !isProjectManager && !isAdmin)
                        return StatusCode(403, "Only the Project Manager or Admin can mark a root task as Completed.");

                    if (task.ParentTaskId != null && !isProjectManager && !isAdmin && !isAssigned)
                        return StatusCode(403, "Only an assigned member, Project Manager, or Admin can mark a subtask as Completed.");
                }

                if (dto.StatusId == 3 && !isAssigned && !isProjectManager && !isAdmin)
                    return StatusCode(403, "Only an assigned member can submit this task for review.");

                var oldStatus = await _context.TaskStatuses.FirstOrDefaultAsync(s => s.Id == task.StatusId);
                var oldStatusName = oldStatus?.Name ?? task.StatusId.ToString();
                var newStatusName = newStatus.Name;

                task.StatusId = dto.StatusId;
                task.UpdatedAt = PhTime;

                var requesterProjectRole = isAdmin ? "Admin" : projectMember?.Role ?? "Unknown";

                _context.AuditLogs.Add(new AuditLog
                {
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    AccountId = requesterId,
                    Action = "UPDATED",
                    OldValue = oldStatusName,
                    NewValue = newStatusName,
                    Note = $"Updated status of task '{task.Title}' to {newStatusName} by {requester.Name} ({requesterProjectRole}).",
					CreatedAt = PhTime
				});
                var assigneeIds = await _context.TaskAssignments
                    .Where(a => a.TaskId == id && !a.IsDeleted)
                    .Select(a => a.AccountId)
                    .ToListAsync();

                foreach (var accountId in assigneeIds)
                {
                    if (accountId == requesterId) continue;

                    await _notificationService.NotifyAsync(
                        accountId,
                        $"Task '{task.Title}' status changed from '{oldStatusName}' to '{newStatusName}'.",
                        projectId: task.ProjectId,
                        taskId: task.Id
                    );

                    var assigneeAccount = await _context.Accounts.FindAsync(accountId);
                    if (assigneeAccount?.Email != null)
                        await _emailService.SendStatusChangedAsync(assigneeAccount.Email, task.Title, newStatusName);
                }

                if (dto.StatusId == 3 && !isProjectManager && !isAdmin)
                {
                    var pmMember = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId &&
                            (m.Role == "Project Manager" || m.Role == "Project Manager - Scrum Master") &&
                            !m.IsDeleted);

                    if (pmMember != null)
                    {
                        await _notificationService.NotifyAsync(
                            pmMember.AccountId,
                            $"Task '{task.Title}' has been submitted for review by {requester.Name}.",
                            projectId: task.ProjectId,
                            taskId: task.Id
                        );
                    }
                }
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

                var deleterProjectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == deleterId && !m.IsDeleted);
                var deleterRole = deleter.Role == "Admin" ? "Admin" : deleterProjectMember?.Role;

                if (deleterRole != "Admin" &&
                    deleterRole != "Project Manager" &&
                    deleterRole != "Scrum Master" &&
                    deleterRole != "Project Manager - Scrum Master")
                    return StatusCode(403, "You do not have permission to delete tasks.");

                await SoftDeleteTaskRecursive(id);

                _context.AuditLogs.Add(new AuditLog
                {
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    AccountId = deleterId,
                    Action = "DELETED",
                    OldValue = task.Title,
                    Note = $"Task '{task.Title}' and all subtasks deleted by {deleter.Name} ({deleterRole}).",
					CreatedAt = PhTime
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
            task.UpdatedAt = PhTime;

            var assignments = await _context.TaskAssignments
                .Where(a => a.TaskId == taskId && !a.IsDeleted)
                .ToListAsync();

            foreach (var assignment in assignments)
            {
                assignment.IsDeleted = true;
                assignment.DeletedAt = PhTime;
            }

            var subtasks = await _context.Tasks
                .Where(t => t.ParentTaskId == taskId && !t.IsDeleted)
                .ToListAsync();

            foreach (var subtask in subtasks)
                await SoftDeleteTaskRecursive(subtask.Id);
        }

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

                    var allowedRoles = new[] { "Project Manager", "Scrum Master", "Project Manager - Scrum Master" };
                    if (!allowedRoles.Contains(projectMember.Role))
                        return StatusCode(403, "Only Admin, Project Manager, or Scrum Master can assign tasks.");

                    if (dto.AssigneeIds.Any())
                    {
                        var validAccountIds = await _context.Accounts
                            .Where(a => dto.AssigneeIds.Contains(a.Id))
                            .Select(a => a.Id)
                            .ToListAsync();

                        var invalidIds = dto.AssigneeIds.Except(validAccountIds).ToList();
                        if (invalidIds.Any())
                            return BadRequest($"The following assignee IDs do not exist: {string.Join(", ", invalidIds)}");
                    }
                }

                var existing = await _context.TaskAssignments
                    .Where(a => a.TaskId == id && !a.IsDeleted)
                    .ToListAsync();

                foreach (var a in existing)
                {
                    a.IsDeleted = true;
                    a.DeletedAt = PhTime;
                }

                var project = await _context.Projects.FindAsync(task.ProjectId);
                foreach (var accountId in dto.AssigneeIds)
                {
                    _context.TaskAssignments.Add(new TaskAssignment
                    {
                        TaskId = id,
                        AccountId = accountId,
                        AssignedById = dto.AssignedById,
                        AssignedAt = PhTime
                    });

                    await _notificationService.NotifyAsync(
                       accountId,
                       $"You have been assigned to task: '{task.Title}' in project '{project?.Name}'.",
                       projectId: task.ProjectId,
                       taskId: task.Id
                    );
                    var assigneeAccount = await _context.Accounts.FindAsync(accountId);
                    if (assigneeAccount?.Email != null)
                        await _emailService.SendTaskAssignedAsync(assigneeAccount.Email, task.Title);

                }

                task.UpdatedAt = PhTime;

                var assignerProjectMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(m => m.ProjectId == task.ProjectId && m.AccountId == dto.AssignedById && !m.IsDeleted);
                var assignerProjectRole = assigner.Role == "Admin" ? "Admin" : assignerProjectMember?.Role;

                _context.AuditLogs.Add(new AuditLog
                {
                    ProjectId = task.ProjectId,
                    TaskId = id,
                    AccountId = dto.AssignedById,
                    Action = "UPDATED",
                    NewValue = string.Join(", ", dto.AssigneeIds),
                    Note = $"Task '{task.Title}' assigned by {assigner.Name} ({assignerProjectRole}).",
					CreatedAt = PhTime
				});

                await _context.SaveChangesAsync();

                var warnings = await CheckWorkloadWarnings(id, dto.AssigneeIds);

                if (warnings.Any())
                {
                    return Ok(new
                    {
                        message = "Task assigned successfully, but some assignees are overloaded.",
                        warnings = warnings
                    });
                }

                return Ok(new { message = "Task assigned successfully." });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("GetTasksByAssignee/{accountId}")]
        public async Task<IActionResult> GetTasksByAssignee(int accountId)
        {
            try
            {
                var tasks = await _context.Tasks
                    .AsNoTracking()
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

                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("GetTasksByProject/{projectId}")]
        public async Task<IActionResult> GetTasksByProject(int projectId, [FromQuery] int requesterId)
        {
            try
            {
                var requester = await _context.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == requesterId);
                if (requester == null)
                    return NotFound("Account not found.");

                IQueryable<TaskItem> query = _context.Tasks
                    .AsNoTracking()
                    .Where(t => t.ProjectId == projectId && !t.IsDeleted);

                if (requester.Role != "Admin")
                {
                    var projectMember = await _context.ProjectMembers
                        .AsNoTracking()
                        .SingleOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == requesterId);

                    if (projectMember == null)
                        return StatusCode(403, "You are not a member of this project.");

                    var isPrivileged = projectMember.Role == "Project Manager" ||
                                       projectMember.Role == "Scrum Master" ||
                                       projectMember.Role == "Project Manager - Scrum Master";

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
                var requesterRole = requester.Role == "Admin" ? "Admin" : requesterProjectMember?.Role;

                var restoredCount = await ReactivateTaskRecursive(taskId);

                _context.AuditLogs.Add(new AuditLog
                {
                    ProjectId = task.ProjectId,
                    TaskId = task.Id,
                    AccountId = requesterId,
                    Action = "RESTORED",
                    NewValue = task.Title,
                    Note = $"Task and all subtasks reactivated by {requester.Name} ({requesterRole}).",
					CreatedAt = PhTime
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
            task.UpdatedAt = PhTime;

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