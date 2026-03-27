namespace TaskManagement.DTOs.Account
{
    public class CreateAccountDTO
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string? Specialization { get; set; }
        public string Role { get; set; }
        public bool isActive { get; set; }
    }
}
