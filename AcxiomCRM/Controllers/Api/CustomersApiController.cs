using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class CustomersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;
        private readonly IAuditService _auditService;

        public CustomersApiController(
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers([FromQuery] string? search)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _context.Customers.Include(c => c.AssignedUser).AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                query = query.Where(c => c.AssignedTo == user.Id);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    c.Phone.Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
            }

            var customers = await query
                .OrderByDescending(c => c.CreatedDate)
                .Select(c => new CustomerDto
                {
                    CustomerId = c.CustomerId,
                    CustomerCode = c.CustomerCode,
                    CustomerName = c.CustomerName,
                    Email = c.Email,
                    Phone = c.Phone,
                    CompanyName = c.CompanyName,
                    Address = c.Address,
                    City = c.City,
                    State = c.State,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    AssignedTo = c.AssignedTo,
                    AssignedUserName = c.AssignedUser != null ? c.AssignedUser.FullName : null
                })
                .ToListAsync();

            return Ok(customers);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerDto>> GetCustomerById(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.AssignedUser)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            if (!_securityService.CanAccessRecord(User, customer.AssignedTo))
            {
                return Forbid();
            }

            var dto = new CustomerDto
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
                CreatedDate = customer.CreatedDate,
                AssignedTo = customer.AssignedTo,
                AssignedUserName = customer.AssignedUser?.FullName
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Uniqueness validation (409 Conflict)
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new { message = "A customer with this email already exists." });
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
            {
                return Conflict(new { message = "A customer with this phone number already exists." });
            }

            var user = await _userManager.GetUserAsync(User);
            var assignedTo = _securityService.IsSalesExecutive(User) ? user?.Id : dto.AssignedTo ?? user?.Id;

            var nextId = (await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 1000) + 1;
            var customerCode = $"CUST-{nextId}";

            var customer = new Customer
            {
                CustomerCode = customerCode,
                CustomerName = dto.CustomerName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                Phone = dto.Phone.Trim(),
                CompanyName = dto.CompanyName?.Trim(),
                Address = dto.Address?.Trim(),
                City = dto.City?.Trim(),
                State = dto.State?.Trim(),
                Status = dto.Status ?? "Active",
                CreatedBy = user?.FullName ?? "API",
                AssignedTo = assignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Create", "Customer", customer.CustomerId.ToString(), null,
                $"API created customer {customer.CustomerName}", "Success");

            var resultDto = new CustomerDto
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
                CreatedDate = customer.CreatedDate,
                AssignedTo = customer.AssignedTo
            };

            return CreatedAtAction(nameof(GetCustomerById), new { id = customer.CustomerId }, resultDto);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            if (!_securityService.CanAccessRecord(User, customer.AssignedTo))
            {
                return Forbid();
            }

            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower() && c.CustomerId != id))
            {
                return Conflict(new { message = "Another customer already uses this email." });
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone && c.CustomerId != id))
            {
                return Conflict(new { message = "Another customer already uses this phone number." });
            }

            customer.CustomerName = dto.CustomerName.Trim();
            customer.Email = dto.Email.Trim().ToLower();
            customer.Phone = dto.Phone.Trim();
            customer.CompanyName = dto.CompanyName?.Trim();
            customer.Address = dto.Address?.Trim();
            customer.City = dto.City?.Trim();
            customer.State = dto.State?.Trim();
            customer.Status = dto.Status ?? customer.Status;
            customer.ModifiedDate = DateTime.UtcNow;

            if (!_securityService.IsSalesExecutive(User) && !string.IsNullOrEmpty(dto.AssignedTo))
            {
                customer.AssignedTo = dto.AssignedTo;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Update", "Customer", customer.CustomerId.ToString(), null,
                $"API updated customer {customer.CustomerName}", "Success");

            return Ok(new { message = "Customer updated successfully." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound(new { message = $"Customer with ID {id} not found." });

            if (!_securityService.CanAccessRecord(User, customer.AssignedTo))
            {
                return Forbid();
            }

            string name = customer.CustomerName;
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("Delete", "Customer", id.ToString(), name, null, "Success", $"API deleted customer {name}");

            return Ok(new { message = $"Customer '{name}' deleted successfully." });
        }
    }
}
