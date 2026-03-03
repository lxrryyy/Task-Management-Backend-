using Microsoft.EntityFrameworkCore.Migrations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
namespace TaskManagement.Models
{
    public class TaskItem
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = "Not Started"; // Not Started, In Progress, Completed, For Review
        public string? Priority { get; set; } // Urgent, Important, Medium, Low 
        public int ReporterId { get; set; }
        public Account Reporter { get; set; }
        public int ProjectId { get; set; }          // Prod Id
        public int? ParentTaskId { get; set; }      // ParentTaskId
        public int? StoryPoints { get; set; } // 1, 2, 3, 4, 5

        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
        public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
        public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
        public ICollection<TaskItem> SubTasks { get; set; } = new List<TaskItem>(); // subtaksss
    }
}
