namespace TaskManagement.DTOs.Account
{
    public class UpdateAccountDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public string? Role { get; set; }
        public bool? isActive { get; set; }

        public string? ProfilePicture { get; set; }
    }
}