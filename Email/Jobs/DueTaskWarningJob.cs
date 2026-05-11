using Microsoft.EntityFrameworkCore;
using TaskManagement.Data;
using TaskManagement.Services;

namespace TaskManagement.Jobs
{
    public class DueTaskWarningJob
    {
        private readonly AccountDbContext _context;
        private readonly IEmailService _emailService;
        private readonly NotificationService _notificationService;

        private static DateTime PhTime =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));

        public DueTaskWarningJob(AccountDbContext context, IEmailService emailService, NotificationService notificationService)
        {
            _context = context;
            _emailService = emailService;
            _notificationService = notificationService;
        }

        public async Task RunAsync()
        {
            var now = PhTime;
            var warningWindow = now.AddHours(24);

            var dueSoonTasks = await _context.Tasks
                .Where(t =>
                    !t.IsDeleted &&
                    t.DueDate.HasValue &&
                    t.DueDate.Value > now &&
                    t.DueDate.Value <= warningWindow &&
                    t.StatusId != 4 &&
                    !t.IsWarningEmailSent)  // ← only tasks not yet warned
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.DueDate,
                    ProjectName = t.Project.Name,
                    Assignees = t.Assignments
                        .Where(a => !a.IsDeleted)
                        .Select(a => new
                        {
                            a.AccountId,
                            a.Account.Name,
                            a.Account.Email
                        })
                        .ToList()
                })
                .ToListAsync();

            foreach (var task in dueSoonTasks)
            {
                var hoursLeft = (int)Math.Ceiling((task.DueDate!.Value - now).TotalHours);
                hoursLeft = Math.Max(hoursLeft, 1);

                foreach (var assignee in task.Assignees)
                {
                    // In-app notification
                    await _notificationService.NotifyAsync(
                        assignee.AccountId,
                        $"⚠️ Task '{task.Title}' is due in ~{hoursLeft} hour{(hoursLeft == 1 ? "" : "s")}.",
                        taskId: task.Id
                    );

                    // Email warning
                    if (!string.IsNullOrEmpty(assignee.Email))
                    {
                        await _emailService.SendTaskDueWarningAsync(
                            assignee.Email,
                            assignee.Name,
                            task.Title,
                            task.ProjectName,
                            task.DueDate.Value,
                            hoursLeft
                        );
                    }
                }

                // Mark as warned AFTER all assignees are notified
                var taskEntity = await _context.Tasks.FindAsync(task.Id);
                if (taskEntity != null)
                {
                    taskEntity.IsWarningEmailSent = true;
                }
            }

            // Save all changes once after the loop
            await _context.SaveChangesAsync();
        }
    }
}