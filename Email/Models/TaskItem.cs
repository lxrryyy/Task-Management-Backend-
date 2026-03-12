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
        public int StatusId { get; set; }       //FK   
        public int? PriorityId { get; set; }       //FK
        public TaskItemStatus Status { get; set; }     //nav
        public TaskPriority? Priority { get; set; }  //nav
        public int CreatorId { get; set; }
        public Account Creator { get; set; }
        public int ProjectId { get; set; } // Prod Id
        public int? ParentTaskId { get; set; } // ParentTaskId
        public int? StoryPoints { get; set; } // 1, 2, 3, 4, 5
        public DateTime? StartDate { get; set; }
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
