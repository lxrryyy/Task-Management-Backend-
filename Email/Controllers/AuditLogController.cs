using ClosedXML.Excel;          
using iTextSharp.text;           
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using System.IO;
using TaskManagement.Data;
using TaskManagement.DTOs.AuditLog;
using TaskManagement.Models;

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
        private IQueryable<AuditLogResponseDTO> BuildLogQuery(IQueryable<AuditLog> source)
        {
            return source.Select(l => new AuditLogResponseDTO
            {
                Id = l.Id,
                ProjectId = l.ProjectId,
                ProjectName = l.ProjectId.HasValue
                    ? _context.Projects.Where(p => p.Id == l.ProjectId).Select(p => p.Name).FirstOrDefault()
                    : null,
                ProjectRole = _context.Accounts
                    .Where(a => a.Id == l.AccountId)
                    .Select(a => a.Role)
                    .FirstOrDefault() == "Admin"
                    ? "Admin"
                    : (l.ProjectId.HasValue
                        ? _context.ProjectMembers
                            .Where(pm => pm.ProjectId == l.ProjectId && pm.AccountId == l.AccountId)
                            .Select(pm => pm.Role)
                            .FirstOrDefault()
                        : null),
                TaskId = l.TaskId,
                AccountId = l.AccountId,
                AccountName = _context.Accounts
                    .Where(a => a.Id == l.AccountId)
                    .Select(a => a.Name)
                    .FirstOrDefault(),
                AccountEmail = _context.Accounts
                    .Where(a => a.Id == l.AccountId)
                    .Select(a => a.Email)
                    .FirstOrDefault(),
                Action = l.Action,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                Note = l.Note,
                CreatedAt = l.CreatedAt
            });
        }
        private async Task<List<AuditLogResponseDTO>> FetchAllLogsAsync(
             int? userId = null,
             string? role = null,
             int? projectId = null,
             string? action = null,
             DateTime? from = null,
             DateTime? to = null)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (userId.HasValue)
                query = query.Where(l => l.AccountId == userId.Value);

            if (!string.IsNullOrEmpty(role))
                query = query.Where(l => _context.Accounts
                    .Where(a => a.Id == l.AccountId)
                    .Select(a => a.Role)
                    .FirstOrDefault() == role);

            if (projectId.HasValue)
                query = query.Where(l => l.ProjectId == projectId.Value);

            if (!string.IsNullOrEmpty(action))
                query = query.Where(l => l.Action == action);

            if (from.HasValue)
                query = query.Where(l => l.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(l => l.CreatedAt <= to.Value.Date.AddDays(1).AddTicks(-1));

            return await BuildLogQuery(
                query.OrderByDescending(l => l.CreatedAt)
            ).ToListAsync();
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

                var logs = await BuildLogQuery(
                    _context.AuditLogs
                        .Where(l => l.TaskId == taskId)
                        .OrderByDescending(l => l.CreatedAt)
                ).ToListAsync();

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

                var logs = await BuildLogQuery(
                    _context.AuditLogs
                        .Where(l => l.AccountId == accountId)
                        .OrderByDescending(l => l.CreatedAt)
                ).ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("GetAllLogs")]
        public async Task<IActionResult> GetAllLogs(
             [FromQuery] int requesterId,
             [FromQuery] int? user_id = null,
             [FromQuery] string? role = null,
             [FromQuery] int? project_id = null,
             [FromQuery] string? action = null,
             [FromQuery] DateTime? from = null,
             [FromQuery] DateTime? to = null)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                if (from.HasValue && to.HasValue && to.Value < from.Value)
                    return BadRequest("'to' date must be on or after 'from' date.");

                var logs = await FetchAllLogsAsync(user_id, role, project_id, action, from, to);
                    
                if (!logs.Any())
                    return NotFound("No audit logs found for the given filters.");

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

                var logs = await BuildLogQuery(
                    _context.AuditLogs
                        .Where(l => l.Action == action)
                        .OrderByDescending(l => l.CreatedAt)
                ).ToListAsync();

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

                var logs = await BuildLogQuery(
                    _context.AuditLogs
                        .Where(l => l.CreatedAt >= from && l.CreatedAt <= to)
                        .OrderByDescending(l => l.CreatedAt)
                ).ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet("ExportExcel")]
        public async Task<IActionResult> ExportExcel(
            [FromQuery] int requesterId,
            [FromQuery] int? user_id = null,
            [FromQuery] string? role = null,
            [FromQuery] int? project_id = null,
            [FromQuery] string? action = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                if (from.HasValue && to.HasValue && to.Value < from.Value)
                    return BadRequest("'to' date must be on or after 'from' date.");

                var logs = await FetchAllLogsAsync(user_id, role, project_id, action, from, to);

                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Audit Logs");

                var rangeLabel = (from.HasValue || to.HasValue)
                    ? $"Date Range: {(from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "Start")} → {(to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "End")}"
                    : "Date Range: All";

                sheet.Cell(1, 1).Value = rangeLabel;
                sheet.Cell(1, 1).Style.Font.Italic = true;
                sheet.Cell(1, 1).Style.Font.FontColor = XLColor.DarkGray;
                sheet.Range(1, 1, 1, 10).Merge();

                var headers = new[]
                {
                    "ID", "Project ID", "Project Name", "Project Role",
                    "Account ID", "Action", "Old Value", "New Value", "Note", "Created At"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = sheet.Cell(2, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F81BD");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    int row = i + 3;
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

                var fileName = from.HasValue || to.HasValue
                    ? $"AuditLogs_{from?.ToString("yyyyMMdd") ?? "Start"}_{to?.ToString("yyyyMMdd") ?? "End"}.xlsx"
                    : $"AuditLogs_All_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }


        [HttpGet("ExportPdf")]
        public async Task<IActionResult> ExportPdf(
             [FromQuery] int requesterId,
             [FromQuery] int? user_id = null,
             [FromQuery] string? role = null,
             [FromQuery] int? project_id = null,
             [FromQuery] string? action = null,
             [FromQuery] DateTime? from = null,
             [FromQuery] DateTime? to = null)
        {
            try
            {
                var (ok, error) = await GuardAdminAsync(requesterId);
                if (!ok) return error!;

                if (from.HasValue && to.HasValue && to.Value < from.Value)
                    return BadRequest("'to' date must be on or after 'from' date.");

                var logs = await FetchAllLogsAsync(user_id, role, project_id, action, from, to);


                using var stream = new MemoryStream();

                var document = new Document(PageSize.A4.Rotate(), 15f, 15f, 20f, 20f);
                var writer = PdfWriter.GetInstance(document, stream);
                writer.CloseStream = false;
                document.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.DARK_GRAY);
                var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.GRAY);

                document.Add(new Paragraph("Audit Log Report", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 4f
                });

                var rangeLabel = (from.HasValue || to.HasValue)
                    ? $"Date Range: {(from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "Start")} → {(to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "End")}"
                    : $"Date Range: All  |  Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";

                document.Add(new Paragraph(rangeLabel, subtitleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 12f
                });

                var table = new PdfPTable(10) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 3f, 5f, 10f, 9f, 5f, 7f, 9f, 9f, 14f, 9f });
                //                            ID  ProjId  ProjName  Role  AccId  Action  OldVal  NewVal  Note  CreatedAt

                var headerBg = new BaseColor(79, 129, 189);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7, BaseColor.WHITE);
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 6, BaseColor.BLACK);
                var altBg = new BaseColor(238, 243, 251);

                foreach (var h in new[]
                {
            "ID", "Project ID", "Project Name", "Project Role",
            "Account ID", "Action", "Old Value", "New Value", "Note", "Created At"
        })
                {
                    table.AddCell(new PdfPCell(new Phrase(h, headerFont))
                    {
                        BackgroundColor = headerBg,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 4f
                    });
                }

                // ── Data rows ─────────────────────────────────────────────────────
                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    var bg = i % 2 == 1 ? altBg : BaseColor.WHITE;

                    foreach (var v in new[]
                    {
                log.Id.ToString(),
                log.ProjectId?.ToString() ?? "-",
                log.ProjectName         ?? "-",
                log.ProjectRole         ?? "-",
                log.AccountId.ToString(),
                log.Action,
                log.OldValue            ?? "-",
                log.NewValue            ?? "-",
                log.Note                ?? "-",
                log.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            })
                    {
                        table.AddCell(new PdfPCell(new Phrase(v, cellFont))
                        {
                            BackgroundColor = bg,
                            Padding = 3f,
                            VerticalAlignment = Element.ALIGN_MIDDLE
                        });
                    }
                }

                document.Add(table);
                document.Close();
                stream.Seek(0, SeekOrigin.Begin);

                var fileName = from.HasValue || to.HasValue
                    ? $"AuditLogs_{from?.ToString("yyyyMMdd") ?? "Start"}_{to?.ToString("yyyyMMdd") ?? "End"}.pdf"
                    : $"AuditLogs_All_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

                return File(stream.ToArray(), "application/pdf", fileName);
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }
    }
}