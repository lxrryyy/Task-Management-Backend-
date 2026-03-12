namespace TaskManagement.DTOs.Task
{
    public class CreateTaskDTO
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? PriorityId { get; set; }
        public int? StoryPoints { get; set; }       
        public int ProjectId { get; set; }          
        public int? ParentTaskId { get; set; }      
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }

        public List<int> AssigneeIds { get; set; } = new List<int>(); // multiple assignees

    }
}
