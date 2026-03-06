namespace TaskManagement.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int StatusId { get; set; }           // fk to project status
        public ProjectStatus Status { get; set; }
        public int CreatedById { get; set; }        // who create project
        public Account CreatedBy { get; set; }
        public int ProjectManagerId { get; set; }   // assigning of project manager
        public int? ScrumMasterId { get; set; }     // assigning of scrum master

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Navigation 
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();


    }
}
