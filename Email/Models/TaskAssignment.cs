using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace TaskManagement.Models
{
    public class TaskAssignment
    {
        [Key]
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int AccountId { get; set; }
        public int AssignedById { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        [ForeignKey("TaskId")]
        public TaskItem Task { get; set; }
        [ForeignKey("AccountId")]
        public Account Account { get; set; }
    }
}
