namespace TaskManagement.DTOs.AuditLog
{
    public class AuditLogResponseDTO
    {
        public int Id { get; set; }
        public int? TaskId { get; set; }
        public int? ProjectId { get; set; }
        public int AccountId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } 
        public string? ProjectName { get; set; }
        public string? ProjectRole { get; set; }
    }
}
