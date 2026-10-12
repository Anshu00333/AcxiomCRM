using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;
        private readonly ICsvExportService _csvExportService;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmSecurityService securityService,
            ICsvExportService csvExportService)
        {
            _context = context;
            _userManager = userManager;
            _securityService = securityService;
            _csvExportService = csvExportService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var customersQuery = _context.Customers.AsQueryable();
            var leadsQuery = _context.Leads.AsQueryable();
            var oppsQuery = _context.Opportunities.AsQueryable();
            var followUpsQuery = _context.FollowUps.AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                customersQuery = customersQuery.Where(c => c.AssignedTo == user.Id);
                leadsQuery = leadsQuery.Where(l => l.AssignedTo == user.Id);
                oppsQuery = oppsQuery.Where(o => o.AssignedTo == user.Id);
                followUpsQuery = followUpsQuery.Where(f => f.AssignedTo == user.Id);
            }

            int totalLeads = await leadsQuery.CountAsync();
            int convertedLeads = await leadsQuery.CountAsync(l => l.Status == "Converted");
            int conversionRate = totalLeads > 0 ? (int)Math.Round((double)convertedLeads * 100 / totalLeads) : 0;

            var openAmounts = await oppsQuery.Where(o => o.Status == "Open").Select(o => o.Amount).ToListAsync();
            var totalPipeline = openAmounts.Sum();

            var vm = new ReportIndexViewModel
            {
                TotalCustomers = await customersQuery.CountAsync(),
                TotalLeads = totalLeads,
                TotalOpportunities = await oppsQuery.CountAsync(),
                TotalPipelineValue = totalPipeline,
                ConversionRatePercent = conversionRate,
                PendingFollowUps = await followUpsQuery.CountAsync(f => f.Status == "Planned")
            };

            return View(vm);
        }

        public async Task<IActionResult> Pipeline()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var oppsQuery = _context.Opportunities
                .Include(o => o.AssignedUser)
                .AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                oppsQuery = oppsQuery.Where(o => o.AssignedTo == user.Id);
            }

            var opps = await oppsQuery.ToListAsync();

            var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var stageSummaries = stages.Select(s =>
            {
                var stageOpps = opps.Where(o => o.Stage == s).ToList();
                return new StageSummaryItem
                {
                    Stage = s,
                    Count = stageOpps.Count,
                    TotalAmount = stageOpps.Sum(o => o.Amount),
                    WeightedAmount = stageOpps.Sum(o => o.WeightedAmount)
                };
            }).ToList();

            var ownerSummaries = opps
                .GroupBy(o => o.AssignedUser?.FullName ?? "Unassigned")
                .Select(g => new OwnerSummaryItem
                {
                    OwnerName = g.Key,
                    OpenCount = g.Count(o => o.Status == "Open"),
                    TotalAmount = g.Where(o => o.Status == "Open").Sum(o => o.Amount),
                    WeightedAmount = g.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount),
                    WonCount = g.Count(o => o.Status == "Won"),
                    WonAmount = g.Where(o => o.Status == "Won").Sum(o => o.Amount)
                }).ToList();

            var vm = new PipelineReportViewModel
            {
                StageSummaries = stageSummaries,
                OwnerSummaries = ownerSummaries,
                GrandTotalPipeline = opps.Where(o => o.Status == "Open").Sum(o => o.Amount),
                GrandTotalWeighted = opps.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount)
            };

            return View(vm);
        }

        public async Task<IActionResult> Conversion()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var leadsQuery = _context.Leads.AsQueryable();
            var oppsQuery = _context.Opportunities.AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                leadsQuery = leadsQuery.Where(l => l.AssignedTo == user.Id);
                oppsQuery = oppsQuery.Where(o => o.AssignedTo == user.Id);
            }

            int totalLeads = await leadsQuery.CountAsync();
            int convertedLeads = await leadsQuery.CountAsync(l => l.Status == "Converted");
            int lostLeads = await leadsQuery.CountAsync(l => l.Status == "Lost");

            int totalOpps = await oppsQuery.CountAsync();
            int wonOpps = await oppsQuery.CountAsync(o => o.Status == "Won");
            int lostOpps = await oppsQuery.CountAsync(o => o.Status == "Lost");

            var vm = new ConversionReportViewModel
            {
                TotalLeads = totalLeads,
                ConvertedLeads = convertedLeads,
                LostLeads = lostLeads,
                ConversionRatePercent = totalLeads > 0 ? Math.Round((decimal)convertedLeads * 100m / totalLeads, 1) : 0,
                TotalOpportunities = totalOpps,
                WonOpportunities = wonOpps,
                LostOpportunities = lostOpps,
                WinRatePercent = (wonOpps + lostOpps) > 0 ? Math.Round((decimal)wonOpps * 100m / (wonOpps + lostOpps), 1) : 0
            };

            return View(vm);
        }

        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> UserActivity()
        {
            var users = await _userManager.Users.Where(u => u.IsActive).ToListAsync();
            var vm = new UserActivityReportViewModel();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                int custCount = await _context.Customers.CountAsync(c => c.AssignedTo == u.Id);
                int leadCount = await _context.Leads.CountAsync(l => l.AssignedTo == u.Id);
                int oppCount = await _context.Opportunities.CountAsync(o => o.AssignedTo == u.Id);
                int completedFollowUps = await _context.FollowUps.CountAsync(f => f.AssignedTo == u.Id && f.Status == "Completed");
                int auditCount = await _context.AuditLogs.CountAsync(a => a.UserId == u.Id);

                vm.UserActivities.Add(new UserActivitySummaryItem
                {
                    UserName = u.FullName,
                    UserEmail = u.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "None",
                    AssignedCustomers = custCount,
                    AssignedLeads = leadCount,
                    AssignedOpportunities = oppCount,
                    CompletedFollowUps = completedFollowUps,
                    TotalAuditActions = auditCount
                });
            }

            return View(vm);
        }

        public async Task<IActionResult> ExportCustomerReport()
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _context.Customers.Include(c => c.AssignedUser).AsQueryable();
            if (_securityService.IsSalesExecutive(User))
                query = query.Where(c => c.AssignedTo == user!.Id);

            var list = await query.OrderBy(c => c.CustomerName).ToListAsync();
            var headers = new[] { "Code", "Name", "Email", "Phone", "Company", "Status", "Owner", "Created Date" };
            var bytes = _csvExportService.GenerateCsvBytes(headers, list, c => new[]
            {
                c.CustomerCode, c.CustomerName, c.Email, c.Phone, c.CompanyName, c.Status, c.AssignedUser?.FullName, c.CreatedDate.ToString("yyyy-MM-dd")
            });
            return File(bytes, "text/csv", $"CustomerReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportLeadReport()
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _context.Leads.Include(l => l.AssignedUser).AsQueryable();
            if (_securityService.IsSalesExecutive(User))
                query = query.Where(l => l.AssignedTo == user!.Id);

            var list = await query.OrderBy(l => l.LeadName).ToListAsync();
            var headers = new[] { "Code", "Name", "Email", "Phone", "Company", "Source", "Status", "Priority", "Expected Value", "Owner" };
            var bytes = _csvExportService.GenerateCsvBytes(headers, list, l => new[]
            {
                l.LeadCode, l.LeadName, l.Email, l.Phone, l.CompanyName, l.Source, l.Status, l.Priority, l.ExpectedValue.ToString("F2"), l.AssignedUser?.FullName
            });
            return File(bytes, "text/csv", $"LeadReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        public async Task<IActionResult> ExportOpportunityReport()
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsQueryable();
            if (_securityService.IsSalesExecutive(User))
                query = query.Where(o => o.AssignedTo == user!.Id);

            var list = await query.OrderBy(o => o.OpportunityName).ToListAsync();
            var headers = new[] { "Name", "Customer", "Amount", "Stage", "Probability", "Weighted Amount", "Close Date", "Status", "Owner" };
            var bytes = _csvExportService.GenerateCsvBytes(headers, list, o => new[]
            {
                o.OpportunityName, o.Customer?.CustomerName, o.Amount.ToString("F2"), o.Stage, $"{o.Probability}%", o.WeightedAmount.ToString("F2"), o.ExpectedCloseDate.ToString("yyyy-MM-dd"), o.Status, o.AssignedUser?.FullName
            });
            return File(bytes, "text/csv", $"OpportunityReport_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
    }
}
