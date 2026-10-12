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
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;
        private readonly IAuditService _auditService;

        public CustomersController(
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

        public async Task<IActionResult> Index(string? searchTerm, string? statusFilter, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var query = _context.Customers
                .Include(c => c.AssignedUser)
                .AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                query = query.Where(c => c.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    c.Phone.Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(c => c.Status == statusFilter);
            }

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var customers = await query
                .OrderByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new CustomerListViewModel
            {
                Customers = customers,
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
            var customer = await _context.Customers
                .Include(c => c.AssignedUser)
                .Include(c => c.Opportunities)
                .Include(c => c.FollowUps)
                .Include(c => c.Activities)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            if (!_securityService.CanAccessRecord(User, customer.AssignedTo))
            {
                return Forbid();
            }

            var vm = new CustomerDetailsViewModel
            {
                Customer = customer,
                Opportunities = customer.Opportunities.OrderByDescending(o => o.CreatedDate).ToList(),
                FollowUps = customer.FollowUps.OrderBy(f => f.FollowUpDate).ToList(),
                Activities = customer.Activities.OrderByDescending(a => a.ActivityDate).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new CustomerCreateEditViewModel
            {
                SalesUsers = await GetSalesUsersSelectListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerCreateEditViewModel model)
        {
            // Business Validation: Email Uniqueness
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "A customer with this email address already exists.");
            }

            // Business Validation: Phone Uniqueness
            if (await _context.Customers.AnyAsync(c => c.Phone == model.Phone))
            {
                ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
            }

            if (!ModelState.IsValid)
            {
                model.SalesUsers = await GetSalesUsersSelectListAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var assignedTo = _securityService.IsSalesExecutive(User) ? user?.Id : model.AssignedTo ?? user?.Id;

            // Generate unique customer code
            var nextId = (await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 1000) + 1;
            var customerCode = $"CUST-{nextId}";

            var customer = new Customer
            {
                CustomerCode = customerCode,
                CustomerName = model.CustomerName.Trim(),
                Email = model.Email.Trim().ToLower(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName?.Trim(),
                Address = model.Address?.Trim(),
                City = model.City?.Trim(),
                State = model.State?.Trim(),
                Status = model.Status,
                CreatedBy = user?.FullName ?? user?.UserName ?? "System",
                AssignedTo = assignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Customer", customer.CustomerId.ToString(), null,
                $"Created customer {customer.CustomerName} ({customer.CustomerCode})", "Success");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, customer.AssignedTo))
            {
                return Forbid();
            }

            var vm = new CustomerCreateEditViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                AssignedTo = customer.AssignedTo,
                SalesUsers = await GetSalesUsersSelectListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CustomerCreateEditViewModel model)
        {
            var customer = await _context.Customers.FindAsync(model.CustomerId);
            if (customer == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, customer.AssignedTo))
            {
                return Forbid();
            }

            // Check uniqueness ignoring current record
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower() && c.CustomerId != model.CustomerId))
            {
                ModelState.AddModelError("Email", "Another customer already uses this email address.");
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == model.Phone && c.CustomerId != model.CustomerId))
            {
                ModelState.AddModelError("Phone", "Another customer already uses this phone number.");
            }

            if (!ModelState.IsValid)
            {
                model.SalesUsers = await GetSalesUsersSelectListAsync();
                return View(model);
            }

            string oldSnapshot = $"Name: {customer.CustomerName}, Email: {customer.Email}, Phone: {customer.Phone}, Status: {customer.Status}";

            customer.CustomerName = model.CustomerName.Trim();
            customer.Email = model.Email.Trim().ToLower();
            customer.Phone = model.Phone.Trim();
            customer.CompanyName = model.CompanyName?.Trim();
            customer.Address = model.Address?.Trim();
            customer.City = model.City?.Trim();
            customer.State = model.State?.Trim();
            customer.Status = model.Status;
            customer.ModifiedDate = DateTime.UtcNow;

            if (!_securityService.IsSalesExecutive(User))
            {
                customer.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            string newSnapshot = $"Name: {customer.CustomerName}, Email: {customer.Email}, Phone: {customer.Phone}, Status: {customer.Status}";
            await _auditService.LogAsync("Update", "Customer", customer.CustomerId.ToString(), oldSnapshot, newSnapshot, "Success", "Customer record updated.");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Opportunities)
                .Include(c => c.FollowUps)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound();

            if (!_securityService.CanAccessRecord(User, customer.AssignedTo))
            {
                return Forbid();
            }

            string customerName = customer.CustomerName;
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Customer", id.ToString(), customerName, null, "Success", $"Deleted customer {customerName}.");

            TempData["SuccessMessage"] = $"Customer '{customerName}' was deleted successfully.";
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
