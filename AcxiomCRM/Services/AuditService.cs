using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;

namespace AcxiomCRM.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action, string entityName, string? recordId = null, string? oldValue = null, string? newValue = null, string result = "Success", string? details = null);
        Task LogAuthAsync(string action, string? userEmail, string? userId, string result, string? details, string? ipAddress);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string entityName, string? recordId = null, string? oldValue = null, string? newValue = null, string result = "Success", string? details = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            string? userId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            string? userEmail = httpContext?.User?.FindFirstValue(ClaimTypes.Email) ?? httpContext?.User?.Identity?.Name;
            string? ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();

            var log = new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = action,
                EntityName = entityName,
                RecordId = recordId,
                OldValue = oldValue,
                NewValue = newValue,
                Result = result,
                Details = details,
                CreatedDate = DateTime.UtcNow,
                IpAddress = ipAddress
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogAuthAsync(string action, string? userEmail, string? userId, string result, string? details, string? ipAddress)
        {
            var log = new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = action,
                EntityName = "Auth",
                RecordId = userId,
                Result = result,
                Details = details,
                CreatedDate = DateTime.UtcNow,
                IpAddress = ipAddress
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
