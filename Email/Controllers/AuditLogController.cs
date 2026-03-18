using TaskManagement.Data;
using TaskManagement.DTOs.AuditLog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;          
using iTextSharp.text;           
using iTextSharp.text.pdf;
using System.IO;

namespace TaskManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogController : ControllerBase
    {
        private readonly AccountDbContext _context;
        public AuditLogController(AccountDbContext context)
        {
            _context = context;
        }

        private async Task<List<AuditLogResponseDTO>> FetchAllLogsAsync()
        {
            return await _context.AuditLogs
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new AuditLogResponseDTO
                {
                    Id = l.Id,
                    ProjectId = l.ProjectId,
                    ProjectName = l.ProjectId.HasValue
                        ? _context.Projects.Where(p => p.Id == l.ProjectId).Select(p => p.Name).FirstOrDefault()
                        : null,
                    ProjectRole = l.ProjectId.HasValue
                        ? _context.ProjectMembers.Where(pm => pm.ProjectId == l.ProjectId && pm.AccountId == l.AccountId).Select(pm => pm.Role).FirstOrDefault()
                        : null,
                    AccountId = l.AccountId,
                    Action = l.Action,
                    OldValue = l.OldValue,
                    NewValue = l.NewValue,
                    Note = l.Note,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();
        }

        private async Task<(bool ok, IActionResult? error)> GuardAdminAsync(int requesterId)
        {
            var requester = await _context.Accounts.FindAsync(requesterId);
            if (requester == null)
                return (false, NotFound("Account not found."));
            if (requester.Role != "Admin")
                return (false, StatusCode(403, "Only Admins can view audit logs."));
            return (true, null);
        }


        [HttpGet("GetTaskLogs/{taskId}")]
        public async Task<IActionResult> GetTaskLogs(int taskId, [FromQuery] int requesterId)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                var logs = await _context.AuditLogs
                    .Where(l => l.TaskId == taskId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new AuditLogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("GetUserLogs/{accountId}")]
        public async Task<IActionResult> GetUserLogs(int accountId, [FromQuery] int requesterId)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                var logs = await _context.AuditLogs
                    .Where(l => l.AccountId == accountId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new AuditLogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("GetAllLogs")]
        public async Task<IActionResult> GetAllLogs([FromQuery] int requesterId)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                var logs = await FetchAllLogsAsync();
                return Ok(logs);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("GetLogsByAction")]
        public async Task<IActionResult> GetLogsByAction([FromQuery] string action, [FromQuery] int requesterId)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                var logs = await _context.AuditLogs
                    .Where(l => l.Action == action)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new AuditLogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("GetLogsByDateRange")]
        public async Task<IActionResult> GetLogsByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] int requesterId)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                var logs = await _context.AuditLogs
                    .Where(l => l.CreatedAt >= from && l.CreatedAt <= to)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new AuditLogResponseDTO
                    {
                        Id = l.Id,
                        TaskId = l.TaskId,
                        AccountId = l.AccountId,
                        Action = l.Action,
                        OldValue = l.OldValue,
                        NewValue = l.NewValue,
                        Note = l.Note,
                        CreatedAt = l.CreatedAt
                    })
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("ExportExcel")]
        public async Task<IActionResult> ExportExcel([FromQuery] int requesterId)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                var logs = await FetchAllLogsAsync();

                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Audit Logs");

                var headers = new[]
                {
                    "ID", "Project ID", "Project Name", "Project Role",
                    "Account ID", "Action", "Old Value", "New Value", "Note", "Created At"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = sheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F81BD");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    int row = i + 2;
                    sheet.Cell(row, 1).Value = log.Id;
                    sheet.Cell(row, 2).Value = log.ProjectId?.ToString() ?? "-";
                    sheet.Cell(row, 3).Value = log.ProjectName ?? "-";
                    sheet.Cell(row, 4).Value = log.ProjectRole ?? "-";
                    sheet.Cell(row, 5).Value = log.AccountId;
                    sheet.Cell(row, 6).Value = log.Action;
                    sheet.Cell(row, 7).Value = log.OldValue ?? "-";
                    sheet.Cell(row, 8).Value = log.NewValue ?? "-";
                    sheet.Cell(row, 9).Value = log.Note ?? "-";
                    sheet.Cell(row, 10).Value = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

                    if (i % 2 == 1)
                        sheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF3FB");
                }

                sheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Seek(0, SeekOrigin.Begin);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"AuditLogs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx"
                );
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

    
        [HttpGet("ExportPdf")]
        public async Task<IActionResult> ExportPdf([FromQuery] int requesterId)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                var logs = await FetchAllLogsAsync();

                using var stream = new MemoryStream();
                var document = new Document(PageSize.A4.Rotate(), 20f, 20f, 20f, 20f);
                var writer = PdfWriter.GetInstance(document, stream);
                writer.CloseStream = false;
                document.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.DARK_GRAY);
                document.Add(new Paragraph($"Audit Log Report — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 12f
                });

                var table = new PdfPTable(7) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 4f, 14f, 10f, 8f, 12f, 12f, 10f });

                var headerBg = new BaseColor(79, 129, 189);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, BaseColor.WHITE);
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 7, BaseColor.BLACK);
                var altBg = new BaseColor(238, 243, 251);

                foreach (var h in new[] { "ID", "Project", "Role", "Account ID", "Action", "Old → New", "Created At" })
                {
                    var cell = new PdfPCell(new Phrase(h, headerFont))
                    {
                        BackgroundColor = headerBg,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 5f
                    };
                    table.AddCell(cell);
                }

                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    var bg = i % 2 == 1 ? altBg : BaseColor.WHITE;

                    var values = new[]
                    {
                        log.Id.ToString(),
                        log.ProjectName ?? "-",
                        log.ProjectRole ?? "-",
                        log.AccountId.ToString(),
                        log.Action,
                        $"{log.OldValue ?? "-"} → {log.NewValue ?? "-"}",
                        log.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                    };

                    foreach (var v in values)
                    {
                        table.AddCell(new PdfPCell(new Phrase(v, cellFont))
                        {
                            BackgroundColor = bg,
                            Padding = 4f
                        });
                    }
                }

                document.Add(table);
                document.Close();
                stream.Seek(0, SeekOrigin.Begin);

                return File(
                    stream.ToArray(),
                    "application/pdf",
                    $"AuditLogs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf"
                );
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }
    }
}