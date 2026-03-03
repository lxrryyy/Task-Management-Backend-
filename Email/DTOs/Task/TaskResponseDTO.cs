namespace TaskManagement.DTOs.Task
{
    public class TaskResponseDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Priority { get; set; }
        public int? StoryPoints { get; set; }
        public int ReporterId { get; set; }
        public string? ReporterName { get; set; }
        public int ProjectId { get; set; }     // 👈 add
        public int? ParentTaskId { get; set; } // 👈 add
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<int> AssigneeIds { get; set; } = new List<int>();
    }
}
