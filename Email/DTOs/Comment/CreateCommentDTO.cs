namespace TaskManagement.DTOs.Comment
{
    public class CreateCommentDTO
    {
        public int TaskId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
