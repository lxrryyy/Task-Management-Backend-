using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Models
{
    public class TimeLog
    {
        [Key]
        public int Id { get; set; }
        public int? ProjectId { get; set; }
        public int? TaskId { get; set; }
        public int AccountId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}