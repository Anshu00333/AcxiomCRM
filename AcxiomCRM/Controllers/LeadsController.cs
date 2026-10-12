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
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;
        private readonly IAuditService _auditService;

        private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"
        };

        public LeadsController(
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

        public async Task<IActionResult> Index(string? searchTerm, string? statusFilter, string? sourceFilter, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var query = _context.Leads
                .Include(l => l.AssignedUser)
                .AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                query = query.Where(l => l.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(l =>
                    l.LeadName.ToLower().Contains(term) ||
                    l.Email.ToLower().Contains(term) ||
                    l.Phone.Contains(term) ||
                    (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(l => l.Status == statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(sourceFilter))
            {
                query = query.Where(l => l.Source == sourceFilter);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var leads = await query
                .OrderByDescending(l => l.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new LeadListViewModel
            {
                Leads = leads,
                SearchTerm = searchTerm,
                StatusFilter = statusFilter,
                SourceFilter = sourceFilter,
                PageIndex = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(vm);
        }

        public async Task<IActionResult> Details(int id)
        {
            var lead = await _context.Leads
                .Include(l => l.AssignedUser)
                .Include(l => l.ConvertedCustomer)
                .Include(l => l.FollowUps)
                .Include(l => l.Activities)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, lead.AssignedTo))
            {
                return Forbid();
            }

            var vm = new LeadDetailsViewModel
            {
                Lead = lead,
                FollowUps = lead.FollowUps.OrderBy(f => f.FollowUpDate).ToList(),
                Activities = lead.Activities.OrderByDescending(a => a.ActivityDate).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new LeadCreateEditViewModel
            {
                SalesUsers = await GetSalesUsersSelectListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeadCreateEditViewModel model)
        {
            // Business rule: status must belong to valid status set
            if (!ValidStatuses.Contains(model.Status))
            {
                ModelState.AddModelError("Status", $"Status must be one of: {string.Join(", ", ValidStatuses)}");
            }

            if (!ModelState.IsValid)
            {
                model.SalesUsers = await GetSalesUsersSelectListAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var assignedTo = _securityService.IsSalesExecutive(User) ? user?.Id : model.AssignedTo ?? user?.Id;

            var nextId = (await _context.Leads.MaxAsync(l => (int?)l.LeadId) ?? 2000) + 1;
            var leadCode = $"LEAD-{nextId}";

            var lead = new Lead
            {
                LeadCode = leadCode,
                LeadName = model.LeadName.Trim(),
                Email = model.Email.Trim().ToLower(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName?.Trim(),
                Source = model.Source,
                Status = model.Status,
                Priority = model.Priority,
                ExpectedValue = model.ExpectedValue,
                Notes = model.Notes?.Trim(),
                AssignedTo = assignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Lead", lead.LeadId.ToString(), null,
                $"Created lead {lead.LeadName} ({lead.LeadCode}) with value {lead.ExpectedValue:C}", "Success");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, lead.AssignedTo))
            {
                return Forbid();
            }

            var vm = new LeadCreateEditViewModel
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Source = lead.Source,
                Status = lead.Status,
                Priority = lead.Priority,
                ExpectedValue = lead.ExpectedValue,
                Notes = lead.Notes,
                AssignedTo = lead.AssignedTo,
                SalesUsers = await GetSalesUsersSelectListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(LeadCreateEditViewModel model)
        {
            var lead = await _context.Leads.FindAsync(model.LeadId);
            if (lead == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, lead.AssignedTo))
            {
                return Forbid();
            }

            // Workflow check: cannot change status back from Converted
            if (lead.Status == "Converted" && model.Status != "Converted")
            {
                ModelState.AddModelError("Status", "A converted lead cannot be reverted to an active status.");
            }

            if (!ValidStatuses.Contains(model.Status))
            {
                ModelState.AddModelError("Status", $"Status must be one of: {string.Join(", ", ValidStatuses)}");
            }

            if (!ModelState.IsValid)
            {
                model.SalesUsers = await GetSalesUsersSelectListAsync();
                return View(model);
            }

            string oldSnapshot = $"Status: {lead.Status}, Value: {lead.ExpectedValue}, Priority: {lead.Priority}";

            lead.LeadName = model.LeadName.Trim();
            lead.Email = model.Email.Trim().ToLower();
            lead.Phone = model.Phone.Trim();
            lead.CompanyName = model.CompanyName?.Trim();
            lead.Source = model.Source;
            lead.Status = model.Status;
            lead.Priority = model.Priority;
            lead.ExpectedValue = model.ExpectedValue;
            lead.Notes = model.Notes?.Trim();

            if (!_securityService.IsSalesExecutive(User))
            {
                lead.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            string newSnapshot = $"Status: {lead.Status}, Value: {lead.ExpectedValue}, Priority: {lead.Priority}";
            await _auditService.LogAsync("Update", "Lead", lead.LeadId.ToString(), oldSnapshot, newSnapshot, "Success", "Lead record updated.");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Convert(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, lead.AssignedTo))
            {
                return Forbid();
            }

            if (lead.Status == "Converted")
            {
                TempData["ErrorMessage"] = "This lead has already been converted.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var vm = new ConvertLeadViewModel
            {
                LeadId = lead.LeadId,
                LeadName = lead.LeadName,
                CompanyName = lead.CompanyName,
                Email = lead.Email,
                Phone = lead.Phone,
                CustomerName = lead.CompanyName ?? lead.LeadName,
                OpportunityName = $"{lead.CompanyName ?? lead.LeadName} - Deal",
                OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 10000m,
                OpportunityStage = "Qualification",
                Probability = 30,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(ConvertLeadViewModel model)
        {
            var lead = await _context.Leads.FindAsync(model.LeadId);
            if (lead == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, lead.AssignedTo))
            {
                return Forbid();
            }

            if (model.CreateOpportunity && model.OpportunityAmount <= 0)
            {
                ModelState.AddModelError("OpportunityAmount", "Opportunity Amount must be greater than 0.");
            }

            if (model.CreateOpportunity && model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            Customer? createdCustomer = null;
            Opportunity? createdOpportunity = null;

            // 1. Create Customer if selected
            if (model.CreateCustomer)
            {
                var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == lead.Email.ToLower());
                if (existingCustomer != null)
                {
                    createdCustomer = existingCustomer;
                }
                else
                {
                    var nextCustId = (await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 1000) + 1;
                    createdCustomer = new Customer
                    {
                        CustomerCode = $"CUST-{nextCustId}",
                        CustomerName = string.IsNullOrWhiteSpace(model.CustomerName) ? lead.LeadName : model.CustomerName.Trim(),
                        Email = lead.Email,
                        Phone = lead.Phone,
                        CompanyName = lead.CompanyName,
                        Status = "Active",
                        CreatedBy = user?.FullName ?? "Conversion",
                        AssignedTo = lead.AssignedTo,
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.Customers.Add(createdCustomer);
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync("Create", "Customer", createdCustomer.CustomerId.ToString(), null,
                        $"Created from lead conversion #{lead.LeadCode}", "Success");
                }
                lead.ConvertedCustomerId = createdCustomer.CustomerId;
            }

            // 2. Create Opportunity if selected
            if (model.CreateOpportunity)
            {
                createdOpportunity = new Opportunity
                {
                    OpportunityName = string.IsNullOrWhiteSpace(model.OpportunityName) ? $"{lead.LeadName} Deal" : model.OpportunityName.Trim(),
                    CustomerId = createdCustomer?.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.OpportunityAmount,
                    Stage = model.OpportunityStage,
                    Probability = model.Probability,
                    ExpectedCloseDate = model.ExpectedCloseDate,
                    Status = "Open",
                    AssignedTo = lead.AssignedTo,
                    CreatedDate = DateTime.UtcNow,
                    Notes = $"Converted from lead {lead.LeadCode} ({lead.LeadName})"
                };
                _context.Opportunities.Add(createdOpportunity);
                await _context.SaveChangesAsync();

                lead.ConvertedOpportunityId = createdOpportunity.OpportunityId;

                await _auditService.LogAsync("Create", "Opportunity", createdOpportunity.OpportunityId.ToString(), null,
                    $"Created opportunity from lead conversion #{lead.LeadCode} with amount {createdOpportunity.Amount:C}", "Success");
            }

            // 3. Mark Lead as Converted
            lead.Status = "Converted";
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Update", "Lead", lead.LeadId.ToString(), "Status: Qualified", "Status: Converted",
                "Success", $"Lead converted to customer #{createdCustomer?.CustomerCode} and opportunity #{createdOpportunity?.OpportunityId}.");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' successfully converted!";
            return RedirectToAction(nameof(Details), new { id = lead.LeadId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var lead = await _context.Leads
                .Include(l => l.FollowUps)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, lead.AssignedTo))
            {
                return Forbid();
            }

            string leadName = lead.LeadName;
            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Lead", id.ToString(), leadName, null, "Success", $"Deleted lead {leadName}.");

            TempData["SuccessMessage"] = $"Lead '{leadName}' was deleted successfully.";
            return RedirectToAction(nameof(Index));
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
