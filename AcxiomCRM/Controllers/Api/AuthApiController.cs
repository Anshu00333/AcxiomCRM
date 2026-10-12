using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public AuthApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input payload.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
            }

            var user = await _userManager.FindByEmailAsync(dto.Email);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (user == null || !user.IsActive)
            {
                await _auditService.LogAuthAsync("Login", dto.Email, user?.Id, "Failure", "API login failed (user not found or deactivated).", ipAddress);
                return Unauthorized(new LoginResponseDto { Success = false, Message = "Invalid credentials or inactive account." });
            }

            var result = await _signInManager.PasswordSignInAsync(dto.Email, dto.Password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAuthAsync("Login", dto.Email, user.Id, "Success", "API login successful.", ipAddress);

                return Ok(new LoginResponseDto
                {
                    Success = true,
                    Message = "Authentication successful.",
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = roles.FirstOrDefault()
                });
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAuthAsync("Failed Login", dto.Email, user.Id, "Warning", "API account locked out.", ipAddress);
                return StatusCode(StatusCodes.Status423Locked, new LoginResponseDto
                {
                    Success = false,
                    Message = "Account is locked due to multiple failed login attempts. Try again later."
                });
            }

            await _auditService.LogAuthAsync("Failed Login", dto.Email, user.Id, "Failure", "Invalid API login attempt.", ipAddress);
            return Unauthorized(new LoginResponseDto { Success = false, Message = "Invalid credentials." });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _signInManager.SignOutAsync();
            await _auditService.LogAuthAsync("Logout", user?.Email, user?.Id, "Success", "API logout successful.", ipAddress);

            return Ok(new { success = true, message = "Logged out successfully." });
        }
    }
}
