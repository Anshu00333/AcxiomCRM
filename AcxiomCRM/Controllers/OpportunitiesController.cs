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
    public class OpportunitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;
        private readonly IAuditService _auditService;

        private static readonly HashSet<string> ValidStages = new(StringComparer.OrdinalIgnoreCase)
        {
            "Qualification", "Proposal", "Negotiation", "Won", "Lost"
        };

        public OpportunitiesController(
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

        public async Task<IActionResult> Index(string? searchTerm, string? stageFilter, string? statusFilter, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                query = query.Where(o => o.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(o =>
                    o.OpportunityName.ToLower().Contains(term) ||
                    (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(term)) ||
                    (o.Notes != null && o.Notes.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(stageFilter))
            {
                query = query.Where(o => o.Stage == stageFilter);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(o => o.Status == statusFilter);
            }

            var totalPipeline = await query.Where(o => o.Status == "Open").SumAsync(o => (decimal?)o.Amount) ?? 0m;
            var weightedPipeline = await query.Where(o => o.Status == "Open").SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m;

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var opportunities = await query
                .OrderByDescending(o => o.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new OpportunityListViewModel
            {
                Opportunities = opportunities,
                SearchTerm = searchTerm,
                StageFilter = stageFilter,
                StatusFilter = statusFilter,
                TotalPipelineValue = totalPipeline,
                WeightedPipelineValue = weightedPipeline,
                PageIndex = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(vm);
        }

        public async Task<IActionResult> Pipeline()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedUser)
                .AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                query = query.Where(o => o.AssignedTo == user.Id);
            }

            var all = await query.ToListAsync();

            var vm = new OpportunityPipelineViewModel
            {
                QualificationStage = all.Where(o => o.Stage == "Qualification").ToList(),
                ProposalStage = all.Where(o => o.Stage == "Proposal").ToList(),
                NegotiationStage = all.Where(o => o.Stage == "Negotiation").ToList(),
                WonStage = all.Where(o => o.Stage == "Won").ToList(),
                LostStage = all.Where(o => o.Stage == "Lost").ToList(),
                TotalPipelineValue = all.Where(o => o.Status == "Open").Sum(o => o.Amount),
                WeightedPipelineValue = all.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount)
            };

            return View(vm);
        }

        public async Task<IActionResult> Details(int id)
        {
            var opp = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .Include(o => o.FollowUps)
                .Include(o => o.Activities)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (opp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, opp.AssignedTo))
            {
                return Forbid();
            }

            var vm = new OpportunityDetailsViewModel
            {
                Opportunity = opp,
                FollowUps = opp.FollowUps.OrderBy(f => f.FollowUpDate).ToList(),
                Activities = opp.Activities.OrderByDescending(a => a.ActivityDate).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            var vm = new OpportunityCreateEditViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                Customers = await GetCustomersSelectListAsync(),
                Leads = await GetLeadsSelectListAsync(),
                SalesUsers = await GetSalesUsersSelectListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OpportunityCreateEditViewModel model)
        {
            // Mandatory Business Validation: Amount > 0
            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            // Mandatory Business Validation: Probability 0 - 100
            if (model.Probability < 0 || model.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            // Mandatory Business Validation: Expected Close Date cannot be in the past for active opportunity
            if (model.Status != "Lost" && model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (!ValidStages.Contains(model.Stage))
            {
                ModelState.AddModelError("Stage", "Invalid opportunity pipeline stage.");
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

            string status = model.Stage == "Won" ? "Won" : model.Stage == "Lost" ? "Lost" : "Open";

            var opp = new Opportunity
            {
                OpportunityName = model.OpportunityName.Trim(),
                CustomerId = model.CustomerId,
                LeadId = model.LeadId,
                Amount = model.Amount,
                Stage = model.Stage,
                Probability = model.Probability,
                ExpectedCloseDate = model.ExpectedCloseDate,
                Status = status,
                AssignedTo = assignedTo,
                Notes = model.Notes?.Trim(),
                CreatedDate = DateTime.UtcNow
            };

            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Opportunity", opp.OpportunityId.ToString(), null,
                $"Created opportunity {opp.OpportunityName} for {opp.Amount:C} (Stage: {opp.Stage})", "Success");

            TempData["SuccessMessage"] = $"Opportunity '{opp.OpportunityName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, opp.AssignedTo))
            {
                return Forbid();
            }

            var vm = new OpportunityCreateEditViewModel
            {
                OpportunityId = opp.OpportunityId,
                OpportunityName = opp.OpportunityName,
                CustomerId = opp.CustomerId,
                LeadId = opp.LeadId,
                Amount = opp.Amount,
                Stage = opp.Stage,
                Probability = opp.Probability,
                ExpectedCloseDate = opp.ExpectedCloseDate,
                Status = opp.Status,
                AssignedTo = opp.AssignedTo,
                Notes = opp.Notes,
                Customers = await GetCustomersSelectListAsync(),
                Leads = await GetLeadsSelectListAsync(),
                SalesUsers = await GetSalesUsersSelectListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OpportunityCreateEditViewModel model)
        {
            var opp = await _context.Opportunities.FindAsync(model.OpportunityId);
            if (opp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, opp.AssignedTo))
            {
                return Forbid();
            }

            // Mandatory Business Validation: Amount > 0
            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            // Mandatory Business Validation: Probability 0 - 100
            if (model.Probability < 0 || model.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            // Mandatory Business Validation: Expected Close Date cannot be in the past for active opportunity
            if (model.Status == "Open" && model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (!ValidStages.Contains(model.Stage))
            {
                ModelState.AddModelError("Stage", "Invalid opportunity pipeline stage.");
            }

            if (!ModelState.IsValid)
            {
                model.Customers = await GetCustomersSelectListAsync();
                model.Leads = await GetLeadsSelectListAsync();
                model.SalesUsers = await GetSalesUsersSelectListAsync();
                return View(model);
            }

            string oldSnapshot = $"Stage: {opp.Stage}, Amount: {opp.Amount}, Prob: {opp.Probability}%, Status: {opp.Status}";

            opp.OpportunityName = model.OpportunityName.Trim();
            opp.CustomerId = model.CustomerId;
            opp.LeadId = model.LeadId;
            opp.Amount = model.Amount;
            opp.Stage = model.Stage;
            opp.Probability = model.Probability;
            opp.ExpectedCloseDate = model.ExpectedCloseDate;
            opp.Status = model.Stage == "Won" ? "Won" : model.Stage == "Lost" ? "Lost" : model.Status;
            opp.Notes = model.Notes?.Trim();

            if (!_securityService.IsSalesExecutive(User))
            {
                opp.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            string newSnapshot = $"Stage: {opp.Stage}, Amount: {opp.Amount}, Prob: {opp.Probability}%, Status: {opp.Status}";
            await _auditService.LogAsync("Update", "Opportunity", opp.OpportunityId.ToString(), oldSnapshot, newSnapshot,
                "Success", "Opportunity record updated.");

            TempData["SuccessMessage"] = $"Opportunity '{opp.OpportunityName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, opp.AssignedTo))
            {
                return Forbid();
            }

            string oppName = opp.OpportunityName;
            _context.Opportunities.Remove(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Opportunity", id.ToString(), oppName, null, "Success", $"Deleted opportunity {oppName}.");

            TempData["SuccessMessage"] = $"Opportunity '{oppName}' was deleted successfully.";
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
