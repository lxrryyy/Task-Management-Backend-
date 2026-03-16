using Microsoft.EntityFrameworkCore.Migrations;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;

namespace TaskManagement.Models
{
    public class Notification
    {

		[Key]
        public int Id { get; set; }
        public int AccountId { get; set; }
        public int? TaskId { get; set; }
        [Required]
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; }
    }
}
