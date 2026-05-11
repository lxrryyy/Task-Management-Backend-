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
        public async Task SendTaskDueWarningAsync(string to, string recipientName, string taskTitle, string projectName, DateTime dueDate, int hoursLeft)
        {
            var isUrgent = hoursLeft <= 6;
            var accentColor = isUrgent ? "#EF4444" : "#F59E0B";
            var badgeBg = isUrgent ? "#FEE2E2" : "#FEF3C7";
            var badgeText = isUrgent ? "#991B1B" : "#92400E";
            var urgencyLabel = isUrgent ? "URGENT — Due Very Soon" : "Due Soon";
            var emoji = isUrgent ? "🚨" : "⚠️";

            var subject = $"{emoji} Task Due in {hoursLeft}h: {taskTitle}";
            var body = $@"
                <div style='font-family: Segoe UI, Arial, sans-serif; max-width: 600px; margin: 40px auto; background: #fff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.08);'>
                    <div style='background: {accentColor}; padding: 28px 32px;'>
                        <h1 style='color: #fff; margin: 0; font-size: 20px;'>{emoji} Task {urgencyLabel}</h1>
                    </div>
                    <div style='padding: 32px; color: #333;'>
                        <p style='font-size: 15px;'>Hello <strong>{recipientName}</strong>,</p>
                        <p style='font-size: 15px;'>A task assigned to you is due within <strong>{hoursLeft} hour{(hoursLeft == 1 ? "" : "s")}</strong>. Please take action.</p>
                        <div style='background: #f8f9fc; border-left: 4px solid {accentColor}; border-radius: 4px; padding: 14px 18px; margin: 20px 0;'>
                            <p style='margin: 6px 0; font-size: 14px;'><strong style='color: #666; min-width: 110px; display: inline-block;'>Task:</strong> {taskTitle}</p>
                            <p style='margin: 6px 0; font-size: 14px;'><strong style='color: #666; min-width: 110px; display: inline-block;'>Project:</strong> {projectName}</p>
                            <p style='margin: 6px 0; font-size: 14px;'><strong style='color: #666; min-width: 110px; display: inline-block;'>Due Date:</strong> {dueDate:MMMM dd, yyyy hh:mm tt}</p>
                            <p style='margin: 6px 0; font-size: 14px;'><strong style='color: #666; min-width: 110px; display: inline-block;'>Time Left:</strong>
                                <span style='background: {badgeBg}; color: {badgeText}; padding: 3px 12px; border-radius: 20px; font-size: 12px; font-weight: 600;'>~{hoursLeft}h remaining</span>
                            </p>
                        </div>
                        <p style='font-size: 15px;'>Please ensure this task is completed or submitted for review before the deadline.</p>
                    </div>
                    <div style='background: #f4f6f9; padding: 18px 32px; text-align: center; font-size: 12px; color: #aaa; border-top: 1px solid #e8eaed;'>
                        This is an automated message from the Task Management System. Please do not reply.
                    </div>
                </div>";

            await SendEmailAsync(to, subject, body);
        }
        public async Task SendCommentNotificationAsync(string toEmail, string recipientName, string commenterName, string taskTitle, string commentContent)
        {
            var subject = $"💬 New Comment on Task: {taskTitle}";
            var body = $@"
                <div style='font-family: Segoe UI, Arial, sans-serif; max-width: 600px; margin: 40px auto; background: #fff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.08);'>
                    <div style='background: #4F46E5; padding: 28px 32px;'>
                        <h1 style='color: #fff; margin: 0; font-size: 20px;'>💬 New Comment on Your Task</h1>
                    </div>
                    <div style='padding: 32px; color: #333;'>
                        <p style='font-size: 15px;'>Hello <strong>{recipientName}</strong>,</p>
                        <p style='font-size: 15px;'><strong>{commenterName}</strong> left a comment on task <strong>{taskTitle}</strong>.</p>
                        <div style='background: #f8f9fc; border-left: 4px solid #4F46E5; border-radius: 4px; padding: 14px 18px; margin: 20px 0;'>
                            <p style='margin: 6px 0; font-size: 14px;'><strong style='color: #666;'>Task:</strong> {taskTitle}</p>
                            <p style='margin: 6px 0; font-size: 14px;'><strong style='color: #666;'>Comment:</strong> {commentContent}</p>
                        </div>
                        <p style='font-size: 15px;'>Please log in to view and reply to the comment.</p>
                    </div>
                    <div style='background: #f4f6f9; padding: 18px 32px; text-align: center; font-size: 12px; color: #aaa; border-top: 1px solid #e8eaed;'>
                        This is an automated message from the Task Management System. Please do not reply.
                    </div>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
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

        public async Task SendTaskAssignedAsync(string toEmail, string taskTitle)
        {
            var subject = $"Task Assigned: {taskTitle}";
            var body = $@"
                <h2>You have been assigned a new task</h2>
                <p><strong>Task:</strong> {taskTitle}</p>
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
        public async Task SendAccountCreatedAsync(string email, string name, string password)
        {
            var subject = "Welcome to Task Management - Your Account Credentials";
            var body = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto;'>
                <div style='background-color: #C0392B; padding: 30px; text-align: center;'>
                    <h1 style='color: white; margin: 0;'>Welcome to TASK MANAGEMENT!</h1>
                </div>
                <div style='padding: 30px;'>
                    <p>Dear {name},</p>
                    <p>Your account has been created. Here are your login credentials:</p>
                    <div style='background-color: #f5f5f5; padding: 20px; border-radius: 5px; margin: 20px 0;'>
                        <p><strong>Your Account Credentials:</strong></p>
                        <p>Email: <a href='mailto:{email}'>{email}</a></p>
                        <p>Password: {password}</p>
                    </div>
                 <div style='text-align: center; margin: 30px 0;'>
                    <a href='http://ec2-52-77-117-213.ap-southeast-1.compute.amazonaws.com:1014/login'
                        style='background-color: #C0392B; color: white; padding: 14px 32px; 
                               text-decoration: none; border-radius: 5px; font-size: 16px; 
                               font-weight: bold; display: inline-block;'>
                        Login to Your Account
                    </a>
                </div>
                    <p>For security reasons, we recommend changing your password after your first login.</p>
                    <p style='margin-top: 30px;'>If you have any questions, please don't hesitate to contact us.</p>
                </div>
            </div>";

            await SendEmailAsync(email, subject, body);
        }
    }
}