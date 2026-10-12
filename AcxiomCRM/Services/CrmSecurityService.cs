using System.Security.Claims;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Services
{
    public interface ICrmSecurityService
    {
        string? GetCurrentUserId(ClaimsPrincipal user);
        bool IsAdmin(ClaimsPrincipal user);
        bool IsManager(ClaimsPrincipal user);
        bool IsSalesExecutive(ClaimsPrincipal user);
        bool CanAccessRecord(ClaimsPrincipal user, string? assignedToId);
    }

    public class CrmSecurityService : ICrmSecurityService
    {
        public string? GetCurrentUserId(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public bool IsAdmin(ClaimsPrincipal user)
        {
            return user.IsInRole("Admin");
        }

        public bool IsManager(ClaimsPrincipal user)
        {
            return user.IsInRole("Manager");
        }

        public bool IsSalesExecutive(ClaimsPrincipal user)
        {
            return user.IsInRole("SalesExecutive");
        }

        public bool CanAccessRecord(ClaimsPrincipal user, string? assignedToId)
        {
            if (IsAdmin(user) || IsManager(user))
                return true;

            var currentUserId = GetCurrentUserId(user);
            return !string.IsNullOrEmpty(currentUserId) && currentUserId == assignedToId;
        }
    }
}
