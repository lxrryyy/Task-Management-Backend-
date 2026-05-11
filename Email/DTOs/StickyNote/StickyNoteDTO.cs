namespace TaskManagement.DTOs.StickyNote
{
    public class CreateStickyNoteDTO
    {
        public string Content { get; set; } = string.Empty;
    }

    public class UpdateStickyNoteDTO
    {
        public string? Content { get; set; }
        public bool? IsPinned { get; set; }
    }

    public class StickyNoteResponseDTO
    {
        public int Id { get; set; }
        public string? HashedId { get; set; }
        public int AccountId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}