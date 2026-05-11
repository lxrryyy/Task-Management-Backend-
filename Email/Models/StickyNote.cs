namespace TaskManagement.Models
{
    public class StickyNote
    {

		public int Id { get; set; }
        public int AccountId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsPinned { get; set; } = false;
        public DateTime CreatedAt { get; set; } 
        public DateTime UpdatedAt { get; set; } 
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public Account Account { get; set; } = null!;
    }
}