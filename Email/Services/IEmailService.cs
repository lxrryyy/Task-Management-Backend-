namespace TaskManagement.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendTaskAssignedAsync(string toEmail, string taskTitle, int taskId);
        Task SendStatusChangedAsync(string toEmail, string taskTitle, string newStatus);
        Task SendDeadlineReminderAsync(string toEmail, string taskTitle, DateTime dueDate);

        Task SendOtpAsync(string toEmail, string name, string otp);
    }
}
