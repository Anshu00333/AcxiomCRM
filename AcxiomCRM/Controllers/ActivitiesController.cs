using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ActivitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;
        private readonly IAuditService _auditService;

        public ActivitiesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmSecurityService securityService,
            IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _securityService = securityService;
            _auditService = auditService;
        }

        public async Task<IActionResult> Index(string? typeFilter, string? searchTerm, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var query = _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.AssignedUser)
                .AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                query = query.Where(a => a.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                query = query.Where(a => a.ActivityType == typeFilter);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(a =>
                    a.Subject.ToLower().Contains(term) ||
                    (a.Description != null && a.Description.ToLower().Contains(term)) ||
                    (a.Customer != null && a.Customer.CustomerName.ToLower().Contains(term)) ||
                    (a.Lead != null && a.Lead.LeadName.ToLower().Contains(term)));
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var activities = await query
                .OrderByDescending(a => a.ActivityDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new ActivityListViewModel
            {
                Activities = activities,
                TypeFilter = typeFilter,
                SearchTerm = searchTerm,
                PageIndex = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, string type = "Call")
        {
            var vm = new ActivityCreateEditViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                ActivityType = type,
                ActivityDate = DateTime.Now,
                Customers = await GetCustomersSelectListAsync(),
                Leads = await GetLeadsSelectListAsync(),
                SalesUsers = await GetSalesUsersSelectListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ActivityCreateEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Customers = await GetCustomersSelectListAsync();
                model.Leads = await GetLeadsSelectListAsync();
                model.SalesUsers = await GetSalesUsersSelectListAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var assignedTo = _securityService.IsSalesExecutive(User) ? user?.Id : model.AssignedTo ?? user?.Id;

            var activity = new Activity
            {
                ActivityType = model.ActivityType,
                Subject = model.Subject.Trim(),
                Description = model.Description?.Trim(),
                ActivityDate = model.ActivityDate,
                CustomerId = model.CustomerId,
                LeadId = model.LeadId,
                AssignedTo = assignedTo,
                Status = model.Status
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Activity", activity.ActivityId.ToString(), null,
                $"Logged activity '{activity.Subject}' ({activity.ActivityType})", "Success");

            TempData["SuccessMessage"] = $"Activity '{activity.Subject}' logged successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, activity.AssignedTo))
            {
                return Forbid();
            }

            string subject = activity.Subject;
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Activity", id.ToString(), subject, null, "Success", $"Deleted activity {subject}.");

            TempData["SuccessMessage"] = $"Activity '{subject}' was deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<SelectListItem>> GetCustomersSelectListAsync()
        {
            var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
            return customers.Select(c => new SelectListItem
            {
                Value = c.CustomerId.ToString(),
                Text = $"{c.CustomerName} ({c.CustomerCode})"
            }).ToList();
        }

        private async Task<List<SelectListItem>> GetLeadsSelectListAsync()
        {
            var leads = await _context.Leads.OrderBy(l => l.LeadName).ToListAsync();
            return leads.Select(l => new SelectListItem
            {
                Value = l.LeadId.ToString(),
                Text = $"{l.LeadName} ({l.LeadCode})"
            }).ToList();
        }

        private async Task<List<SelectListItem>> GetSalesUsersSelectListAsync()
        {
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            return users.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FullName} ({u.Email})"
            }).ToList();
        }
    }
}
