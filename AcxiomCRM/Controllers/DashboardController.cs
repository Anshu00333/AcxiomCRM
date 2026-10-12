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

            DateTime now = DateTime.UtcNow;

            // In-memory materialization for SQLite cross-platform reliability
            var allOpportunities = await opportunitiesQuery
                .Include(o => o.Customer)
                .ToListAsync();

            var allLeads = await leadsQuery.ToListAsync();
            var totalCustomersCount = await customersQuery.CountAsync();
            var allFollowUps = await followUpsQuery
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .ToListAsync();

            var allActivities = await activitiesQuery
                .Include(a => a.Customer)
                .ToListAsync();

            var vm = new DashboardViewModel
            {
                DateFilter = dateFilter,
                UserRole = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Manager") ? "Manager" : "SalesExecutive",
                UserDisplayName = user.FullName ?? user.UserName ?? "User",
                TotalCustomers = totalCustomersCount,
                TotalLeads = allLeads.Count,
                OpenLeads = allLeads.Count(l => l.Status != "Lost" && l.Status != "Converted"),
                TotalOpportunities = allOpportunities.Count,
                OpenOpportunities = allOpportunities.Count(o => o.Status == "Open"),
                WonOpportunities = allOpportunities.Count(o => o.Status == "Won"),
                LostOpportunities = allOpportunities.Count(o => o.Status == "Lost"),
                TotalPipelineValue = allOpportunities.Where(o => o.Status == "Open").Sum(o => o.Amount),
                WeightedPipelineValue = allOpportunities.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount),
                PendingFollowUpsCount = allFollowUps.Count(f => f.Status == "Planned" && f.FollowUpDate >= now.Date),
                OverdueFollowUpsCount = allFollowUps.Count(f => f.Status == "Planned" && f.FollowUpDate < now.Date)
            };

            // Chart 1: Lead Status Breakdown
            var leadStatuses = new[] { "New", "Contacted", "Qualified", "Converted", "Lost" };
            vm.LeadStatusLabels = leadStatuses.ToList();
            vm.LeadStatusCounts = leadStatuses.Select(s => allLeads.Count(l => string.Equals(l.Status, s, StringComparison.OrdinalIgnoreCase))).ToList();

            // Chart 2: Opportunity Pipeline Stage Breakdown
            var pipelineStages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            vm.PipelineStageLabels = pipelineStages.ToList();
            vm.PipelineStageValues = pipelineStages.Select(s => allOpportunities.Where(o => string.Equals(o.Stage, s, StringComparison.OrdinalIgnoreCase)).Sum(o => o.Amount)).ToList();

            // Chart 3: Monthly Sales / Won deals trend (Last 6 Months)
            var monthlyLabels = new List<string>();
            var monthlyValues = new List<decimal>();
            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = now.AddMonths(-i);
                var monthStart = new DateTime(targetMonth.Year, targetMonth.Month, 1);
                var monthEnd = monthStart.AddMonths(1);
                var monthWonTotal = allOpportunities
                    .Where(o => o.Status == "Won" && o.CreatedDate >= monthStart && o.CreatedDate < monthEnd)
                    .Sum(o => o.Amount);

                monthlyLabels.Add(targetMonth.ToString("MMM yyyy"));
                monthlyValues.Add(monthWonTotal);
            }
            vm.MonthlySalesLabels = monthlyLabels;
            vm.MonthlySalesValues = monthlyValues;

            // Recent Leads & Opportunities
            vm.RecentLeads = allLeads
                .OrderByDescending(l => l.CreatedDate)
                .Take(5)
                .ToList();

            vm.RecentOpportunities = allOpportunities
                .OrderByDescending(o => o.CreatedDate)
                .Take(5)
                .ToList();

            vm.UpcomingFollowUps = allFollowUps
                .Where(f => f.Status == "Planned")
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToList();

            vm.RecentActivities = allActivities
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .ToList();

            return View(vm);
        }
    }
}
