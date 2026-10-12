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
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;

        public DashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmSecurityService securityService)
        {
            _context = context;
            _userManager = userManager;
            _securityService = securityService;
        }

        public async Task<IActionResult> Index(string dateFilter = "ThisMonth")
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = user.Id;
            var isSalesExecutive = _securityService.IsSalesExecutive(User);

            // Base queries scoped by role
            var customersQuery = _context.Customers.AsQueryable();
            var leadsQuery = _context.Leads.AsQueryable();
            var opportunitiesQuery = _context.Opportunities.AsQueryable();
            var followUpsQuery = _context.FollowUps.AsQueryable();
            var activitiesQuery = _context.Activities.AsQueryable();

            if (isSalesExecutive)
            {
                customersQuery = customersQuery.Where(c => c.AssignedTo == userId);
                leadsQuery = leadsQuery.Where(l => l.AssignedTo == userId);
                opportunitiesQuery = opportunitiesQuery.Where(o => o.AssignedTo == userId);
                followUpsQuery = followUpsQuery.Where(f => f.AssignedTo == userId);
                activitiesQuery = activitiesQuery.Where(a => a.AssignedTo == userId);
            }

            // Date filtering for time-sensitive stats
            DateTime now = DateTime.UtcNow;
            DateTime? filterStartDate = dateFilter switch
            {
                "Today" => now.Date,
                "ThisWeek" => now.Date.AddDays(-(int)now.DayOfWeek),
                "ThisMonth" => new DateTime(now.Year, now.Month, 1),
                _ => null
            };

            var vm = new DashboardViewModel
            {
                DateFilter = dateFilter,
                UserRole = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Manager") ? "Manager" : "SalesExecutive",
                UserDisplayName = user.FullName ?? user.UserName ?? "User",
                TotalCustomers = await customersQuery.CountAsync(),
                TotalLeads = await leadsQuery.CountAsync(),
                OpenLeads = await leadsQuery.CountAsync(l => l.Status != "Lost" && l.Status != "Converted"),
                TotalOpportunities = await opportunitiesQuery.CountAsync(),
                OpenOpportunities = await opportunitiesQuery.CountAsync(o => o.Status == "Open"),
                WonOpportunities = await opportunitiesQuery.CountAsync(o => o.Status == "Won"),
                LostOpportunities = await opportunitiesQuery.CountAsync(o => o.Status == "Lost"),
                TotalPipelineValue = await opportunitiesQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)o.Amount) ?? 0m,
                WeightedPipelineValue = await opportunitiesQuery.Where(o => o.Status == "Open").SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m,
                PendingFollowUpsCount = await followUpsQuery.CountAsync(f => f.Status == "Planned" && f.FollowUpDate >= now.Date),
                OverdueFollowUpsCount = await followUpsQuery.CountAsync(f => f.Status == "Planned" && f.FollowUpDate < now.Date)
            };

            // Chart 1: Lead Status Breakdown
            var leadStatuses = new[] { "New", "Contacted", "Qualified", "Converted", "Lost" };
            var leadStatusData = await leadsQuery
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            vm.LeadStatusLabels = leadStatuses.ToList();
            vm.LeadStatusCounts = leadStatuses.Select(s => leadStatusData.FirstOrDefault(d => d.Status == s)?.Count ?? 0).ToList();

            // Chart 2: Opportunity Pipeline Stage Breakdown
            var pipelineStages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var pipelineData = await opportunitiesQuery
                .GroupBy(o => o.Stage)
                .Select(g => new { Stage = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            vm.PipelineStageLabels = pipelineStages.ToList();
            vm.PipelineStageValues = pipelineStages.Select(s => pipelineData.FirstOrDefault(d => d.Stage == s)?.Total ?? 0m).ToList();

            // Chart 3: Monthly Sales / Won deals trend (Last 6 Months)
            var monthlyLabels = new List<string>();
            var monthlyValues = new List<decimal>();
            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = now.AddMonths(-i);
                var monthStart = new DateTime(targetMonth.Year, targetMonth.Month, 1);
                var monthEnd = monthStart.AddMonths(1);
                var monthWonTotal = await opportunitiesQuery
                    .Where(o => o.Status == "Won" && o.CreatedDate >= monthStart && o.CreatedDate < monthEnd)
                    .SumAsync(o => (decimal?)o.Amount) ?? 0m;

                monthlyLabels.Add(targetMonth.ToString("MMM yyyy"));
                monthlyValues.Add(monthWonTotal);
            }
            vm.MonthlySalesLabels = monthlyLabels;
            vm.MonthlySalesValues = monthlyValues;

            // Recent Leads & Opportunities
            vm.RecentLeads = await leadsQuery
                .OrderByDescending(l => l.CreatedDate)
                .Take(5)
                .ToListAsync();

            vm.RecentOpportunities = await opportunitiesQuery
                .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedDate)
                .Take(5)
                .ToListAsync();

            vm.UpcomingFollowUps = await followUpsQuery
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Where(f => f.Status == "Planned")
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToListAsync();

            vm.RecentActivities = await activitiesQuery
                .Include(a => a.Customer)
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .ToListAsync();

            return View(vm);
        }
    }
}
