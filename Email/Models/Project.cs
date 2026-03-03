namespace TaskManagement.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = "Not Started"; // Active / Not Started / Completed

        public int CreatedById { get; set; }        // who create project
        public Account CreatedBy { get; set; }
        public int ProjectManagerId { get; set; }   // assigning of project manager
        public int? ScrumMasterId { get; set; }     // assigning of scrum master

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        // Navigation 
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
