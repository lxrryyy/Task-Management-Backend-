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
                        Status = p.Status,
                        CreatedById = p.CreatedById,
                        CreatedByName = p.CreatedBy.Name,
                        ProjectManagerId = p.ProjectManagerId,
                        ScrumMasterId = p.ScrumMasterId,
                        MemberIds = p.Members.Select(m => m.AccountId).ToList(),
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync();

                if (!projects.Any())
                    return NotFound("No projects found created by this account.");

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

                int projectManagerId;
                int? scrumMasterId;

                if (creator.Role == "Admin")
                {
                    // Admin must select a project manager
                    if (dto.ProjectManagerId == null)
                        return BadRequest("Admin must select a Project Manager.");

                    projectManagerId = dto.ProjectManagerId.Value;
                    scrumMasterId = dto.ScrumMasterId;
                }
                else
                {
                    // User automatically becomes Project Manager
                    projectManagerId = creatorId;

                    // User can also be Scrum Master at the same time
                    if (dto.IsAlsoScrumMaster)
                        scrumMasterId = creatorId;
                    else
                        scrumMasterId = dto.ScrumMasterId;
                }

                // Create the project
                var project = new Project
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    CreatedById = creatorId,
                    ProjectManagerId = projectManagerId,
                    ScrumMasterId = scrumMasterId,
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
                // If user is both PM and SM
                else if (scrumMasterId != null && scrumMasterId == projectManagerId)
                {
                    // Update the PM member role to reflect both roles
                    var pmMember = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == project.Id && m.AccountId == projectManagerId);
                    if (pmMember != null)
                        pmMember.Role = "ProjectManager-ScrumMaster";
                }

                if (dto.MemberIds.Distinct().Count() != dto.MemberIds.Count)
                    return BadRequest("Duplicate member IDs are not allowed.");

                // Add Members
                foreach (var memberId in dto.MemberIds.Distinct())
                {
                    // Skip if already added as PM or SM
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

                // Log it
                _context.TimeLogs.Add(new TimeLog
                {
                    TaskId = null,
                    AccountId = creatorId,
                    Action = "ProjectCreated",
                    NewValue = project.Name,
                    Note = $"Project created by {creator.Role}"
                });

                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, new ProjectResponseDTO
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    Status = project.Status,
                    CreatedById = project.CreatedById,
                    CreatedByName = creator.Name,
                    ProjectManagerId = project.ProjectManagerId,
                    ScrumMasterId = project.ScrumMasterId,
                    MemberIds = dto.MemberIds,
                    CreatedAt = project.CreatedAt,
                    UpdatedAt = project.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    inner2 = ex.InnerException?.InnerException?.Message
                });

            }
        }

        // PATCH - Project Manager/Scrum/Admin updates the project
        [HttpPatch("UpdateProject/{projectId}")]
        public async Task<IActionResult> UpdateProject(int projectId, [FromBody] UpdateProjectDTO dto, [FromQuery] int requesterId)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null || project.IsDeleted)
                    return NotFound("Project not found.");

                // Check if requester is the Project Manager or Admin
                var requester = await _context.Accounts.FindAsync(requesterId);
                if (requester == null)
                    return NotFound("Requester account not found.");

                // Check if requester is Project Manager of this project
                var projectMember = await _context.ProjectMembers
                    .SingleOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == requesterId);

                var isAdmin = requester.Role == "Admin";
                var isProjectManager = projectMember?.Role == "ProjectManager" ||
                                       projectMember?.Role == "ProjectManager-ScrumMaster";

                if (!isAdmin && !isProjectManager)
                    return StatusCode(403, "Only the Project Manager or Admin can update this project.");

                var changes = new List<string>();

                // Update basic fields
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
                if (dto.Status != null && dto.Status != project.Status)
                {
                    changes.Add($"Status: {project.Status} → {dto.Status}");
                    project.Status = dto.Status;
                }

                // Update Project Manager
                if (dto.ProjectManagerId.HasValue && dto.ProjectManagerId != project.ProjectManagerId)
                {
                    // Remove old PM role
                    var oldPm = await _context.ProjectMembers
                        .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == project.ProjectManagerId);
                    if (oldPm != null)
                        _context.ProjectMembers.Remove(oldPm);

                    // Add new PM
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
                    // Remove old SM role
                    if (project.ScrumMasterId.HasValue)
                    {
                        var oldSm = await _context.ProjectMembers
                            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.AccountId == project.ScrumMasterId);
                        if (oldSm != null && oldSm.Role == "ScrumMaster")
                            _context.ProjectMembers.Remove(oldSm);
                    }

                    // Add new SM
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
                if (dto.MemberIds != null)
                {
                    // Validate no duplicates
                    if (dto.MemberIds.Distinct().Count() != dto.MemberIds.Count)
                        return BadRequest("Duplicate member IDs are not allowed.");

                    // Remove existing members only (not PM or SM)
                    var existingMembers = await _context.ProjectMembers
                        .Where(m => m.ProjectId == projectId && m.Role == "Member")
                        .ToListAsync();
                    _context.ProjectMembers.RemoveRange(existingMembers);

                    // Add new members
                    foreach (var memberId in dto.MemberIds.Distinct())
                    {
                        var alreadyAdded = await _context.ProjectMembers
                            .AnyAsync(m => m.ProjectId == projectId && m.AccountId == memberId);

                        if (!alreadyAdded)
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
                    changes.Add($"Members updated");
                }

                project.UpdatedAt = DateTime.UtcNow;

                // Log changes
                if (changes.Any())
                {
                    _context.TimeLogs.Add(new TimeLog
                    {
                        TaskId = null,
                        AccountId = requesterId,
                        Action = "ProjectUpdated",
                        NewValue = string.Join(", ", changes),
                        Note = $"Project updated by {requester.Role}"
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
                        Status = p.Status,
                        CreatedById = p.CreatedById,
                        CreatedByName = p.CreatedBy.Name,
                        ProjectManagerId = p.ProjectManagerId,
                        ScrumMasterId = p.ScrumMasterId,
                        MemberIds = p.Members.Select(m => m.AccountId).ToList(),
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync();

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
                        Status = p.Status,
                        CreatedById = p.CreatedById,
                        ProjectManagerId = p.ProjectManagerId,
                        ScrumMasterId = p.ScrumMasterId,
                        MemberIds = p.Members.Select(m => m.AccountId).ToList(),
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .FirstOrDefaultAsync();

                if (project == null)
                    return NotFound("Project not found.");

                return Ok(project);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET my projects for checking if i am an assignee or member of the project
        [HttpGet("GetMyProjects/{accountId}")]
        public async Task<IActionResult> GetMyProjects(int accountId)
        {
            try
            {
                var myProjectIds = await _context.ProjectMembers
                    .Where(m => m.AccountId == accountId)
                    .Select(m => m.ProjectId)
                    .ToListAsync();

                var projects = await _context.Projects
                    .Where(p => myProjectIds.Contains(p.Id) && !p.IsDeleted)
                    .Select(p => new ProjectResponseDTO
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Status = p.Status,
                        CreatedById = p.CreatedById,
                        CreatedByName = p.CreatedBy.Name,
                        ProjectManagerId = p.ProjectManagerId,
                        ScrumMasterId = p.ScrumMasterId,
                        MemberIds = p.Members.Select(m => m.AccountId).ToList(),
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .ToListAsync();

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


        [HttpGet("GetProjectProgress/{projectId}")]
        public async Task<IActionResult> GetProjectProgress(int projectId)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null || project.IsDeleted)
                    return NotFound("Project not found.");

                var allTasks = await _context.Tasks
                    .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                    .ToListAsync();

                // Only count leaf tasks (tasks with no subtasks) - these are the actual work items
                var leafTasks = allTasks
                    .Where(t => !allTasks.Any(sub => sub.ParentTaskId == t.Id))
                    .ToList();

                var totalTasks = leafTasks.Count;
                var completedTasks = leafTasks.Count(t => t.Status == "Completed");
                var inProgressTasks = leafTasks.Count(t => t.Status == "In Progress");
                var notStartedTasks = leafTasks.Count(t => t.Status == "Not Started");
                var forReviewTasks = leafTasks.Count(t => t.Status == "For Review");

                var percentage = totalTasks == 0 ? 0 : Math.Round((double)completedTasks / totalTasks * 100, 2);

                return Ok(new
                {
                    ProjectId = projectId,
                    ProjectName = project.Name,
                    ProjectStatus = project.Status,
                    TotalLeafTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    InProgressTasks = inProgressTasks,
                    NotStartedTasks = notStartedTasks,
                    ForReviewTasks = forReviewTasks,
                    CompletionPercentage = percentage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}