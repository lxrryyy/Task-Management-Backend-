using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Models
{
    public class TaskPriority
    {

		[Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty; // Urgent, Important, Medium, Low
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}