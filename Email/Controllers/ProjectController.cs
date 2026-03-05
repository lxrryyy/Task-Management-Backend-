using TaskManagement.Data;
using TaskManagement.DTOs.Project;
using TaskManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly AccountDbContext _context;

        public ProjectController(AccountDbContext context)
        {
            _context = context;
        }

        private async Task<int> GetProjectCompletionPercentage(int projectId)
        {
            var rootTasks = await _context.Tasks
                .Where(t => t.ProjectId == projectId && !t.IsDeleted && t.ParentTaskId == null)
                .Select(t => new { t.StatusId })
                .ToListAsync();

            if (!rootTasks.Any()) return 0;

            var completed = rootTasks.Count(t => t.StatusId == 4); // 4 = Completed
            return (int)Math.Round((double)completed / rootTasks.Count * 100);
        }

        // GET projects created by user
        [HttpGet("GetProjectsCreatedByMe/{accountId}")]
        public async Task<IActionResult> GetProjectsCreatedByMe(int accountId)
        {
            try
            {
                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                    return NotFound("Account not found.");

                var projects = await _context.Projects
                    .Where(p => p.CreatedById == accountId && !p.IsDeleted)
                    .Select(p => new ProjectResponseDTO
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        StatusId = p.StatusId,
                        StatusName = p.Status.Name,
                        CreatedById = p.CreatedById,
                        CreatedByName = p.CreatedBy.Name,
                        ProjectManagerId = p.ProjectManagerId,
                        ProjectManagerName = p.Members
                            .Where(m => m.AccountId == p.ProjectManagerId)
                            .Select(m => m.Account.Name)
                            .FirstOrDefault(),
                        ScrumMasterId = p.ScrumMasterId,
                        ScrumMasterName = p.Members
                            .Where(m => m.AccountId == p.ScrumMasterId)
                            .Select(m => m.Account.Name)
                            .FirstOrDefault(),
                        MemberNames = p.Members
                            .Select(m => m.Account.Name)
                            .ToList(),
                        StartDate = p.StartDate,
                        EndDate = p.EndDate,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync();

                if (!projects.Any())
                    return NotFound("No projects found created by this account.");

                foreach (var p in projects)
                    p.CompletionPercentage = await GetProjectCompletionPercentage(p.Id);

                return Ok(projects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("CreateProject")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDTO dto, [FromQuery] int creatorId)
        {
            try
            {
                var creator = await _context.Accounts.FindAsync(creatorId);
                if (creator == null)
                    return NotFound("Creator account not found.");

                // Validate dates
                if (dto.StartDate == default)
                    return BadRequest("Start date is required.");
                if (dto.EndDate == default)
                    return BadRequest("End date is required.");
                if (dto.EndDate <= dto.StartDate)
                    return BadRequest("End date must be after start date.");

                int projectManagerId;
                int? scrumMasterId;

                if (creator.Role == "Admin")
                {
                    if (dto.ProjectManagerId == null)
                        return BadRequest("Admin must select a Project Manager.");
                    projectManagerId = dto.ProjectManagerId.Value;
                    scrumMasterId = dto.ScrumMasterId;
                }
                else
                {
                    projectManagerId = creatorId;
                    if (dto.IsAlsoScrumMaster)
                        scrumMasterId = creatorId;
                    else
                        scrumMasterId = dto.ScrumMasterId;
                }

                // Set matic to 1
                var project = new Project
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    CreatedById = creatorId,
                    ProjectManagerId = projectManagerId,
                    ScrumMasterId = scrumMasterId,
                    StatusId = 1, // Not Started, then Active when PM/SM adds first task
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                // Add Project Manager as member
                _context.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = project.Id,
                    AccountId = projectManagerId,
                    Role = "ProjectManager",
                    JoinedAt = DateTime.UtcNow
                });

                // Add Scrum Master as member if different from PM
                if (scrumMasterId != null && scrumMasterId != projectManagerId)
                {
                    _context.ProjectMembers.Add(new ProjectMember
                    {
                        ProjectId = project.Id,
                        AccountId = scrumMasterId.Value,
                        Role = "ScrumMaster",
                        JoinedAt = DateTime.UtcNow
                    });
                }
                else if (scrumMasterId != null && scrumMasterId == projectManagerId)
                {
                    var pmMember = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == project.Id && m.AccountId == projectManagerId);
                    if (pmMember != null)
                        pmMember.Role = "ProjectManager-ScrumMaster";
                }

                if (dto.MemberIds.Distinct().Count() != dto.MemberIds.Count)
                    return BadRequest("Duplicate member IDs are not allowed.");

                foreach (var memberId in dto.MemberIds.Distinct())
                {
                    var alreadyAdded = await _context.ProjectMembers
                        .AnyAsync(m => m.ProjectId == project.Id && m.AccountId == memberId);

                    if (!alreadyAdded)
                    {
                        _context.ProjectMembers.Add(new ProjectMember
                        {
                            ProjectId = project.Id,
                            AccountId = memberId,
                            Role = "Member",
                            JoinedAt = DateTime.UtcNow
                        });
                    }
                }

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = null,
                    AccountId = creatorId,
                    Action = "ProjectCreated",
                    NewValue = project.Name,
                    Note = $"Project created by {creator.Name}"
                });

                await _context.SaveChangesAsync();

                var memberNames = await _context.ProjectMembers
                    .Where(pm => pm.ProjectId == project.Id)
                    .Select(pm => pm.Account.Name)
                    .ToListAsync();

                return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, new ProjectResponseDTO
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    StatusId = project.StatusId,
                    StatusName = "Not Started",
                    CreatedById = project.CreatedById,
                    CreatedByName = creator.Name,
                    ProjectManagerId = project.ProjectManagerId,
                    ScrumMasterId = project.ScrumMasterId,
                    MemberNames = memberNames,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    CreatedAt = project.CreatedAt,
                    UpdatedAt = project.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    inner2 = ex.InnerException?.InnerException?.Message
                });
            }
        }

        [HttpPatch("UpdateProject/{projectId}")]
        public async Task<IActionResult> UpdateProject(int projectId, [FromBody] UpdateProjectDTO dto, [FromQuery] int requesterId)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null || project.IsDeleted)
                    return NotFound("Project not found.");

                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Requester account not found.");

                var projectMember = await _context.ProjectMembers
                    .SingleOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == requesterId);

                var isAdmin = requester.Role == "Admin";
                var isProjectManager = projectMember?.Role == "ProjectManager" ||
                                       projectMember?.Role == "ProjectManager-ScrumMaster";

                if (!isAdmin && !isProjectManager)
                    return StatusCode(403, "Only the Project Manager or Admin can update this project.");

                var changes = new List<string>();

                if (dto.Name != null && dto.Name != project.Name)
                {
                    changes.Add($"Name: {project.Name} → {dto.Name}");
                    project.Name = dto.Name;
                }
                if (dto.Description != null && dto.Description != project.Description)
                {
                    changes.Add($"Description updated");
                    project.Description = dto.Description;
                }

                
                if (dto.StatusId.HasValue && dto.StatusId != project.StatusId)
                {
                
                    var statusExists = await _context.ProjectStatuses.AnyAsync(s => s.Id == dto.StatusId.Value);
                    if (!statusExists)
                        return BadRequest("Invalid StatusId.");

                    changes.Add($"StatusId: {project.StatusId} → {dto.StatusId}");
                    project.StatusId = dto.StatusId.Value;
                }

                // Only Admin can update Project Manager
                if (isAdmin && dto.ProjectManagerId.HasValue && dto.ProjectManagerId != project.ProjectManagerId)
                {
                    var oldPm = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == project.ProjectManagerId);
                    if (oldPm != null)
                        _context.ProjectMembers.Remove(oldPm);

                    var newPmExists = await _context.ProjectMembers
                        .AnyAsync(m => m.ProjectId == projectId && m.AccountId == dto.ProjectManagerId.Value);
                    if (!newPmExists)
                    {
                        _context.ProjectMembers.Add(new ProjectMember
                        {
                            ProjectId = projectId,
                            AccountId = dto.ProjectManagerId.Value,
                            Role = "ProjectManager",
                            JoinedAt = DateTime.UtcNow
                        });
                    }

                    changes.Add($"ProjectManager: {project.ProjectManagerId} → {dto.ProjectManagerId}");
                    project.ProjectManagerId = dto.ProjectManagerId.Value;
                }

                // Update Scrum Master
                if (dto.ScrumMasterId != project.ScrumMasterId)
                {
                    if (project.ScrumMasterId.HasValue)
                    {
                        var oldSm = await _context.ProjectMembers
                            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == project.ScrumMasterId);
                        if (oldSm != null && oldSm.Role == "ScrumMaster")
                            _context.ProjectMembers.Remove(oldSm);
                    }

                    if (dto.ScrumMasterId.HasValue)
                    {
                        var newSmExists = await _context.ProjectMembers
                            .AnyAsync(m => m.ProjectId == projectId && m.AccountId == dto.ScrumMasterId.Value);
                        if (!newSmExists)
                        {
                            _context.ProjectMembers.Add(new ProjectMember
                            {
                                ProjectId = projectId,
                                AccountId = dto.ScrumMasterId.Value,
                                Role = "ScrumMaster",
                                JoinedAt = DateTime.UtcNow
                            });
                        }
                    }

                    changes.Add($"ScrumMaster: {project.ScrumMasterId} → {dto.ScrumMasterId}");
                    project.ScrumMasterId = dto.ScrumMasterId;
                }

                // Update Members
                if (dto.AssigneeIds != null)
                {
                    if (dto.AssigneeIds.Distinct().Count() != dto.AssigneeIds.Count)
                        return BadRequest("Duplicate assignee IDs are not allowed.");

                    var existingMembers = await _context.ProjectMembers
                        .Where(m => m.ProjectId == projectId && m.Role == "Member")
                        .ToListAsync();

                    var toRemove = existingMembers
                        .Where(m => !dto.AssigneeIds.Contains(m.AccountId))
                        .ToList();
                    _context.ProjectMembers.RemoveRange(toRemove);

                    foreach (var memberId in dto.AssigneeIds.Distinct())
                    {
                        var alreadyExists = await _context.ProjectMembers
                            .AnyAsync(m => m.ProjectId == projectId && m.AccountId == memberId);

                        if (!alreadyExists)
                        {
                            _context.ProjectMembers.Add(new ProjectMember
                            {
                                ProjectId = projectId,
                                AccountId = memberId,
                                Role = "Member",
                                JoinedAt = DateTime.UtcNow
                            });
                        }
                    }
                    changes.Add("Assignees updated");
                }

                // Update Dates
                if (dto.StartDate.HasValue && dto.StartDate != project.StartDate)
                {
                    changes.Add($"StartDate: {project.StartDate} → {dto.StartDate}");
                    project.StartDate = dto.StartDate.Value;
                }
                if (dto.EndDate.HasValue && dto.EndDate != project.EndDate)
                {
                    changes.Add($"EndDate: {project.EndDate} → {dto.EndDate}");
                    project.EndDate = dto.EndDate.Value;
                }
                if (dto.StartDate.HasValue && dto.EndDate.HasValue && dto.EndDate <= dto.StartDate)
                    return BadRequest("End date must be after start date.");

                project.UpdatedAt = DateTime.UtcNow;

                if (changes.Any())
                {
                    _context.TimeLogs.Add(new TimeLog
                    {
                        TaskId = null,
                        AccountId = requesterId,
                        Action = "ProjectUpdated",
                        NewValue = string.Join(", ", changes),
                        Note = $"Project updated by {requester.Name}"
                    });
                }

                await _context.SaveChangesAsync();
                return NoContent();
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

        // GET all projects
        [HttpGet("GetAllProjects")]
        public async Task<IActionResult> GetAllProjects()
        {
            try
            {
                var projects = await _context.Projects
                    .Where(p => !p.IsDeleted)
                    .Select(p => new ProjectResponseDTO
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        StatusId = p.StatusId,
                        StatusName = p.Status.Name,                  //RESPONSE BODY ADDED
                        CreatedById = p.CreatedById,
                        CreatedByName = p.CreatedBy.Name,
                        ProjectManagerId = p.ProjectManagerId,
                        ProjectManagerName = p.Members
                            .Where(m => m.AccountId == p.ProjectManagerId)
                            .Select(m => m.Account.Name)
                            .FirstOrDefault(),
                        ScrumMasterId = p.ScrumMasterId,
                        ScrumMasterName = p.Members
                            .Where(m => m.AccountId == p.ScrumMasterId)
                            .Select(m => m.Account.Name)
                            .FirstOrDefault(),
                        MemberNames = p.Members
                            .Select(m => m.Account.Name)
                            .ToList(),
                        StartDate = p.StartDate,
                        EndDate = p.EndDate,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync();

                foreach (var p in projects)
                    p.CompletionPercentage = await GetProjectCompletionPercentage(p.Id);

                return Ok(projects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET project by id
        [HttpGet("GetProjectById/{id}")]
        public async Task<IActionResult> GetProjectById(int id)
        {
            try
            {
                var project = await _context.Projects
                    .Where(p => p.Id == id && !p.IsDeleted)
                    .Select(p => new ProjectResponseDTO
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        StatusId = p.StatusId,
                        StatusName = p.Status.Name, 
                        CreatedById = p.CreatedById,
                        CreatedByName = p.CreatedBy.Name,
                        ProjectManagerId = p.ProjectManagerId,
                        ProjectManagerName = p.Members
                            .Where(m => m.AccountId == p.ProjectManagerId)
                            .Select(m => m.Account.Name)
                            .FirstOrDefault(),
                        ScrumMasterId = p.ScrumMasterId,
                        ScrumMasterName = p.Members
                            .Where(m => m.AccountId == p.ScrumMasterId)
                            .Select(m => m.Account.Name)
                            .FirstOrDefault(),
                        MemberNames = p.Members
                            .Select(m => m.Account.Name)
                            .ToList(),
                        StartDate = p.StartDate,
                        EndDate = p.EndDate,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (project == null)
                    return NotFound("Project not found.");

                project.CompletionPercentage = await GetProjectCompletionPercentage(project.Id);
                return Ok(project);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("GetMyProjects/{accountId}")]
        public async Task<IActionResult> GetMyProjects(int accountId)
        {
            try
            {
                var projects = await _context.Projects
                    .Where(p => !p.IsDeleted && p.Members.Any(m => m.AccountId == accountId))
                    .Select(p => new ProjectResponseDTO
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        StatusId = p.StatusId,
                        StatusName = p.Status.Name, 
                        CreatedById = p.CreatedById,
                        CreatedByName = p.CreatedBy.Name,
                        ProjectManagerId = p.ProjectManagerId,
                        ProjectManagerName = p.Members
                            .Where(m => m.AccountId == p.ProjectManagerId)
                            .Select(m => m.Account.Name)
                            .FirstOrDefault(),
                        ScrumMasterId = p.ScrumMasterId,
                        ScrumMasterName = p.Members
                            .Where(m => m.AccountId == p.ScrumMasterId)
                            .Select(m => m.Account.Name)
                            .FirstOrDefault(),
                        MemberNames = p.Members
                            .Select(m => m.Account.Name)
                            .ToList(),
                        StartDate = p.StartDate,
                        EndDate = p.EndDate,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync();

                foreach (var p in projects)
                    p.CompletionPercentage = await GetProjectCompletionPercentage(p.Id);

                return Ok(projects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // DELETE project (soft delete, admin only)
        [HttpDelete("DeleteProject/{id}")]
        public async Task<IActionResult> DeleteProject(int id, [FromQuery] int adminId)
        {
            try
            {
                var admin = await _context.Accounts.FindAsync(adminId);
                if (admin == null || admin.Role != "Admin")
                    return StatusCode(403, "Access denied. Admins only.");

                var project = await _context.Projects.FindAsync(id);
                if (project == null || project.IsDeleted)
                    return NotFound("Project not found.");

                project.IsDeleted = true;
                project.UpdatedAt = DateTime.UtcNow;

                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = null,
                    AccountId = adminId,
                    Action = "ProjectDeleted",
                    OldValue = project.Name,
                    Note = "Project deleted by admin"
                });

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}