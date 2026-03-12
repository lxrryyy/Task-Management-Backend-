namespace TaskManagement.Models
{
    public class OtpCode
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public string Code { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
