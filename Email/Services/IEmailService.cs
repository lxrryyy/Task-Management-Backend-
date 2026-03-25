namespace TaskManagement.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendTaskAssignedAsync(string toEmail, string taskTitle);
        Task SendStatusChangedAsync(string toEmail, string taskTitle, string newStatus);
        Task SendDeadlineReminderAsync(string toEmail, string taskTitle, DateTime dueDate);

        Task SendOtpAsync(string toEmail, string name, string otp);
        Task SendTaskDueWarningAsync(string to, string recipientName, string taskTitle, string projectName, DateTime dueDate, int hoursLeft);
        Task SendCommentNotificationAsync(string toEmail, string recipientName, string commenterName, string taskTitle, string commentContent);

    }
}
