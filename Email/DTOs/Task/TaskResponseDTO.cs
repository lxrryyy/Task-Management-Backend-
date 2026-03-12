namespace TaskManagement.DTOs.Task
{
    public class TaskResponseDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int StatusId { get; set; }           
        public string StatusName { get; set; } = string.Empty; 
        public int? PriorityId { get; set; }        
        public string? PriorityName { get; set; }   
        public int? StoryPoints { get; set; }
        public int CreatorId { get; set; }
        public string? CreatorName { get; set; }
        public int ProjectId { get; set; }     
        public int? ParentTaskId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<int> AssigneeIds { get; set; } = new List<int>();
    }
}
