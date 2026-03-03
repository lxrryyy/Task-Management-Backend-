namespace TaskManagement.DTOs.Task
{
    public class UpdateTaskStatusDTO
    {
        public string Status { get; set; } = string.Empty;
        // Not started, In Progress, Completed, For Review
    }
}
