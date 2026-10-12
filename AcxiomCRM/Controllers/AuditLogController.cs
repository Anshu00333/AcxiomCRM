using AcxiomCRM.Data;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICsvExportService _csvExportService;

        public AuditLogController(ApplicationDbContext context, ICsvExportService csvExportService)
        {
            _context = context;
            _csvExportService = csvExportService;
        }

        public async Task<IActionResult> Index(string? userFilter, string? moduleFilter, string? actionFilter, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(userFilter))
            {
                var term = userFilter.Trim().ToLower();
                query = query.Where(a => a.UserEmail != null && a.UserEmail.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(moduleFilter))
            {
                query = query.Where(a => a.EntityName == moduleFilter);
            }

            if (!string.IsNullOrWhiteSpace(actionFilter))
            {
                query = query.Where(a => a.Action == actionFilter);
            }

            if (startDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));
            }

            int pageSize = 15;
            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var logs = await query
                .OrderByDescending(a => a.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.UserFilter = userFilter;
            ViewBag.ModuleFilter = moduleFilter;
            ViewBag.ActionFilter = actionFilter;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.PageIndex = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(logs);
        }

        public async Task<IActionResult> ExportCsv(string? userFilter, string? moduleFilter, string? actionFilter, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(userFilter))
                query = query.Where(a => a.UserEmail != null && a.UserEmail.ToLower().Contains(userFilter.Trim().ToLower()));

            if (!string.IsNullOrWhiteSpace(moduleFilter))
                query = query.Where(a => a.EntityName == moduleFilter);

            if (!string.IsNullOrWhiteSpace(actionFilter))
                query = query.Where(a => a.Action == actionFilter);

            if (startDate.HasValue)
                query = query.Where(a => a.CreatedDate >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(a => a.CreatedDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            var logs = await query.OrderByDescending(a => a.CreatedDate).ToListAsync();

            var headers = new[] { "LogId", "Timestamp", "UserEmail", "Action", "Module", "RecordId", "Result", "Details", "IP Address" };
            var bytes = _csvExportService.GenerateCsvBytes(headers, logs, l => new[]
            {
                l.AuditLogId.ToString(),
                l.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                l.UserEmail,
                l.Action,
                l.EntityName,
                l.RecordId,
                l.Result,
                l.Details,
                l.IpAddress
            });

            return File(bytes, "text/csv", $"AcxiomCRM_AuditLog_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }
    }
}
