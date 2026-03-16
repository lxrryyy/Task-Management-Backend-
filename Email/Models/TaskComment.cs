using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Models
{
    public class TaskComment
    {

		[Key]
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int AccountId { get; set; }
        [Required]
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } 
        public DateTime UpdatedAt { get; set; } 
        public bool IsDeleted { get; set; } = false;
    }
}