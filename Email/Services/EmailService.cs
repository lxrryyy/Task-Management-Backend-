using System.Net;
using System.Net.Mail;

namespace TaskManagement.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpHost = _config["Smtp:Host"];
            var smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
            var smtpUser = _config["Smtp:Username"];
            var smtpPass = _config["Smtp:Password"];
            var fromEmail = _config["Smtp:From"];
            var fromName = _config["Smtp:FromName"];

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }

        public async Task SendTaskAssignedAsync(string toEmail, string taskTitle, int taskId)
        {
            var subject = $"Task Assigned: {taskTitle}";
            var body = $@"
                <h2>You have been assigned a new task</h2>
                <p><strong>Task:</strong> {taskTitle}</p>
                <p><strong>Task ID:</strong> {taskId}</p>
                <p>Please log in to view the task details.</p>
            ";
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendStatusChangedAsync(string toEmail, string taskTitle, string newStatus)
        {
            var subject = $"Task Status Updated: {taskTitle}";
            var body = $@"
                <h2>Task Status Changed</h2>
                <p><strong>Task:</strong> {taskTitle}</p>
                <p><strong>New Status:</strong> {newStatus}</p>
                <p>Please log in to view the task details.</p>
            ";
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendDeadlineReminderAsync(string toEmail, string taskTitle, DateTime dueDate)
        {
            var subject = $"Deadline Reminder: {taskTitle}";
            var body = $@"
                <h2>Task Deadline Reminder</h2>
                <p><strong>Task:</strong> {taskTitle}</p>
                <p><strong>Due Date:</strong> {dueDate:MMMM dd, yyyy}</p>
                <p>Please make sure to complete the task before the deadline.</p>
            ";
            await SendEmailAsync(toEmail, subject, body);
        }
        public async Task SendOtpAsync(string toEmail, string name, string otp)
        {
            var subject = "Password Reset OTP";
            var body = $@"
                <h2>Password Reset Request</h2>
                <p>Hello <strong>{name}</strong>,</p>
                <p>Your OTP code for password reset is:</p>
                <h1 style='letter-spacing: 8px; color: #4F46E5;'>{otp}</h1>
                <p>This OTP is valid for <strong>15 minutes</strong>.</p>
                <p>If you did not request this, please ignore this email.</p>
            ";
            await SendEmailAsync(toEmail, subject, body);
        }
    }
}