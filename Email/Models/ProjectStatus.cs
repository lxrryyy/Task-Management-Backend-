using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Models
{
    public class ProjectStatus
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty; // Active, Not Started, Completed
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}