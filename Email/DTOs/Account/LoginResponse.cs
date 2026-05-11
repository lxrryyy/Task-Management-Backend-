namespace TaskManagement.DTOs.Account
{
    public class LoginResponse
    {
        public bool Success { get; set; }
        public int ExpiresIn { get; set; }
        public int? UserId { get; set; }
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? Message { get; set; }
    }
}
