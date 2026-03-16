using TaskManagement.Models;

namespace TaskManagement.DTOs.Task
{
    public class UpdateTaskDTO
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? StatusId { get; set; }        
        public int? PriorityId { get; set; }
        public int? StoryPoints { get; set; }
        public DateTime StartDate { get; set; }

        public int ProjectId { get; set; }          // task belongs to a project
        public int? ParentTaskId { get; set; }      // null = main task, has value = subtask
        public ICollection<TaskItem> SubTasks { get; set; } = new List<TaskItem>(); // child tasks
    }
}
