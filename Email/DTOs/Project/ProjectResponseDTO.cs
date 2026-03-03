namespace TaskManagement.DTOs.Project
{
    public class ProjectResponseDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public int CreatedById { get; set; }
        public string? CreatedByName { get; set; }
        public int ProjectManagerId { get; set; }
        public int? ScrumMasterId { get; set; }
        public List<int> MemberIds { get; set; } = new List<int>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
