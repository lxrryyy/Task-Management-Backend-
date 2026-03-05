namespace TaskManagement.Models
{
    public class ProjectMember
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int AccountId { get; set; }
        public string Role { get; set; } = "Member"; // Member, ScrumMaster, ProjectManager
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public Account Account { get; set; }
    }
}
