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
    public class FollowUpsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;
        private readonly IAuditService _auditService;

        public FollowUpsController(
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

        public async Task<IActionResult> Index(string filterType = "All", string? searchTerm = null, string? statusFilter = null, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedUser)
                .AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                query = query.Where(f => f.AssignedTo == user.Id);
            }

            var now = DateTime.UtcNow;
            var today = now.Date;

            query = filterType switch
            {
                "Planned" => query.Where(f => f.Status == "Planned"),
                "Today" => query.Where(f => f.Status == "Planned" && f.FollowUpDate.Date == today),
                "Overdue" => query.Where(f => f.Status == "Planned" && f.FollowUpDate.Date < today),
                "Completed" => query.Where(f => f.Status == "Completed"),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(f =>
                    f.Subject.ToLower().Contains(term) ||
                    (f.Remarks != null && f.Remarks.ToLower().Contains(term)) ||
                    (f.Customer != null && f.Customer.CustomerName.ToLower().Contains(term)) ||
                    (f.Lead != null && f.Lead.LeadName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(f => f.Status == statusFilter);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var followUps = await query
                .OrderBy(f => f.FollowUpDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new FollowUpListViewModel
            {
                FollowUps = followUps,
                FilterType = filterType,
                SearchTerm = searchTerm,
                StatusFilter = statusFilter,
                PageIndex = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(vm);
        }

        public async Task<IActionResult> Details(int id)
        {
            var followUp = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedUser)
                .FirstOrDefaultAsync(f => f.FollowUpId == id);

            if (followUp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, followUp.AssignedTo))
            {
                return Forbid();
            }

            return View(followUp);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            var vm = new FollowUpCreateEditViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                RelatedType = customerId.HasValue ? "Customer" : "Lead",
                FollowUpDate = DateTime.Now.Date.AddDays(1).AddHours(10),
                Customers = await GetCustomersSelectListAsync(),
                Leads = await GetLeadsSelectListAsync(),
                SalesUsers = await GetSalesUsersSelectListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUpCreateEditViewModel model)
        {
            // Mandatory Business Validation: Follow-up date cannot be earlier than today for planned follow-up
            if (model.Status == "Planned" && model.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
            }

            if (!ModelState.IsValid)
            {
                model.Customers = await GetCustomersSelectListAsync();
                model.Leads = await GetLeadsSelectListAsync();
                model.SalesUsers = await GetSalesUsersSelectListAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var assignedTo = _securityService.IsSalesExecutive(User) ? user?.Id : model.AssignedTo ?? user?.Id;

            var followUp = new FollowUp
            {
                CustomerId = model.CustomerId,
                LeadId = model.LeadId,
                RelatedType = model.RelatedType,
                FollowUpDate = model.FollowUpDate,
                FollowUpType = model.FollowUpType,
                Subject = model.Subject.Trim(),
                Remarks = model.Remarks?.Trim(),
                Status = model.Status,
                AssignedTo = assignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "FollowUp", followUp.FollowUpId.ToString(), null,
                $"Scheduled follow-up '{followUp.Subject}' ({followUp.FollowUpType}) for {followUp.FollowUpDate:d}", "Success");

            TempData["SuccessMessage"] = $"Follow-up '{followUp.Subject}' scheduled successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, followUp.AssignedTo))
            {
                return Forbid();
            }

            var vm = new FollowUpCreateEditViewModel
            {
                FollowUpId = followUp.FollowUpId,
                CustomerId = followUp.CustomerId,
                LeadId = followUp.LeadId,
                RelatedType = followUp.RelatedType,
                FollowUpDate = followUp.FollowUpDate,
                FollowUpType = followUp.FollowUpType,
                Subject = followUp.Subject,
                Remarks = followUp.Remarks,
                Status = followUp.Status,
                AssignedTo = followUp.AssignedTo,
                Customers = await GetCustomersSelectListAsync(),
                Leads = await GetLeadsSelectListAsync(),
                SalesUsers = await GetSalesUsersSelectListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FollowUpCreateEditViewModel model)
        {
            var followUp = await _context.FollowUps.FindAsync(model.FollowUpId);
            if (followUp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, followUp.AssignedTo))
            {
                return Forbid();
            }

            // Mandatory Business Validation: Follow-up date cannot be earlier than today for planned follow-up
            if (model.Status == "Planned" && model.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
            }

            if (!ModelState.IsValid)
            {
                model.Customers = await GetCustomersSelectListAsync();
                model.Leads = await GetLeadsSelectListAsync();
                model.SalesUsers = await GetSalesUsersSelectListAsync();
                return View(model);
            }

            string oldSnapshot = $"Date: {followUp.FollowUpDate:d}, Status: {followUp.Status}";

            followUp.CustomerId = model.CustomerId;
            followUp.LeadId = model.LeadId;
            followUp.RelatedType = model.RelatedType;
            followUp.FollowUpDate = model.FollowUpDate;
            followUp.FollowUpType = model.FollowUpType;
            followUp.Subject = model.Subject.Trim();
            followUp.Remarks = model.Remarks?.Trim();
            followUp.Status = model.Status;

            if (!_securityService.IsSalesExecutive(User))
            {
                followUp.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            string newSnapshot = $"Date: {followUp.FollowUpDate:d}, Status: {followUp.Status}";
            await _auditService.LogAsync("Update", "FollowUp", followUp.FollowUpId.ToString(), oldSnapshot, newSnapshot,
                "Success", "Rescheduled/updated follow-up.");

            TempData["SuccessMessage"] = $"Follow-up '{followUp.Subject}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Complete(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, followUp.AssignedTo))
            {
                return Forbid();
            }

            var vm = new CompleteFollowUpViewModel
            {
                FollowUpId = followUp.FollowUpId,
                Subject = followUp.Subject,
                Remarks = followUp.Remarks,
                NextFollowUpDate = DateTime.Now.Date.AddDays(7).AddHours(10)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(CompleteFollowUpViewModel model)
        {
            var followUp = await _context.FollowUps.FindAsync(model.FollowUpId);
            if (followUp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, followUp.AssignedTo))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            followUp.Status = "Completed";
            followUp.CompletedDate = DateTime.UtcNow;
            followUp.Remarks = string.IsNullOrEmpty(followUp.Remarks)
                ? $"Outcome: {model.OutcomeNotes}"
                : $"{followUp.Remarks}\n\nOutcome ({DateTime.UtcNow:g}): {model.OutcomeNotes}";

            // Also record an Activity
            var activity = new Activity
            {
                ActivityType = followUp.FollowUpType,
                Subject = $"Completed: {followUp.Subject}",
                Description = model.OutcomeNotes,
                ActivityDate = DateTime.UtcNow,
                CustomerId = followUp.CustomerId,
                LeadId = followUp.LeadId,
                AssignedTo = followUp.AssignedTo,
                Status = "Completed"
            };
            _context.Activities.Add(activity);

            // If user checked schedule next
            if (model.ScheduleNext && model.NextFollowUpDate.HasValue)
            {
                var nextFollowUp = new FollowUp
                {
                    CustomerId = followUp.CustomerId,
                    LeadId = followUp.LeadId,
                    RelatedType = followUp.RelatedType,
                    FollowUpDate = model.NextFollowUpDate.Value,
                    FollowUpType = model.NextFollowUpType ?? "Call",
                    Subject = string.IsNullOrWhiteSpace(model.NextSubject) ? $"Next follow-up on {followUp.Subject}" : model.NextSubject.Trim(),
                    Status = "Planned",
                    AssignedTo = followUp.AssignedTo,
                    CreatedDate = DateTime.UtcNow
                };
                _context.FollowUps.Add(nextFollowUp);
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Update", "FollowUp", followUp.FollowUpId.ToString(), "Status: Planned", "Status: Completed",
                "Success", $"Completed follow-up with outcome notes: {model.OutcomeNotes}");

            TempData["SuccessMessage"] = $"Follow-up '{followUp.Subject}' marked as completed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, followUp.AssignedTo))
            {
                return Forbid();
            }

            string subject = followUp.Subject;
            _context.FollowUps.Remove(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "FollowUp", id.ToString(), subject, null, "Success", $"Deleted follow-up {subject}.");

            TempData["SuccessMessage"] = $"Follow-up '{subject}' was deleted successfully.";
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
