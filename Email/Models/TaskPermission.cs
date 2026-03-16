using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Models
{
    public class TaskPermission
    {

        [Key]
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int AccountId { get; set; }
        public bool CanView { get; set; } = true;
        public bool CanEdit { get; set; } = false;
        public bool CanDelete { get; set; } = false;
        public bool CanComment { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}