using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService _auditService,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            this._auditService = _auditService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (user != null && !user.IsActive)
            {
                await _auditService.LogAuthAsync("Login", model.Email, user.Id, "Failure", "Account is deactivated by administrator.", ipAddress);
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact your administrator.");
                return View(model);
            }

            // Lockout on failure enabled
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _auditService.LogAuthAsync("Login", model.Email, user?.Id, "Success", "Successful login.", ipAddress);
                _logger.LogInformation("User {Email} logged in successfully.", model.Email);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAuthAsync("Failed Login", model.Email, user?.Id, "Warning", "Account locked out due to multiple failed login attempts.", ipAddress);
                _logger.LogWarning("User {Email} account is locked out.", model.Email);
                ModelState.AddModelError(string.Empty, "This account is temporarily locked due to repeated failed login attempts. Please try again after 15 minutes or contact your administrator.");
                return View(model);
            }

            // Invalid credentials
            await _auditService.LogAuthAsync("Failed Login", model.Email, user?.Id, "Failure", "Invalid email or password.", ipAddress);
            ModelState.AddModelError(string.Empty, "Invalid login credentials. Please verify your email and password.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Department = model.Department,
                EmailConfirmed = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (createResult.Succeeded)
            {
                // Default registered role is SalesExecutive for self-registration
                string assignedRole = !string.IsNullOrEmpty(model.Role) ? model.Role : "SalesExecutive";
                if (assignedRole != "SalesExecutive" && assignedRole != "Manager" && assignedRole != "Admin")
                {
                    assignedRole = "SalesExecutive";
                }

                await _userManager.AddToRoleAsync(user, assignedRole);

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _auditService.LogAuthAsync("Create", model.Email, user.Id, "Success", $"User registered with role {assignedRole}.", ipAddress);

                // Sign the user in
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Dashboard");
            }

            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _auditService.LogAuthAsync("Logout", user?.Email, user?.Id, "Success", "User logged out.", ipAddress);
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
