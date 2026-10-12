using System.Security.Claims;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Xunit;

namespace AcxiomCRM.Tests
{
    public class RoleAndWorkflowTests
    {
        private ClaimsPrincipal CreateClaimsPrincipal(string userId, string email, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public void Scenario9_SalesExecutive_CanOnlyAccessAssignedRecords()
        {
            var securityService = new CrmSecurityService();
            var salesUser = CreateClaimsPrincipal("sales-user-1", "sales@acxiomcrm.com", "SalesExecutive");

            // Sales executive should access their own record
            bool canAccessOwn = securityService.CanAccessRecord(salesUser, "sales-user-1");
            Assert.True(canAccessOwn);

            // Sales executive cannot access another rep's record
            bool canAccessOther = securityService.CanAccessRecord(salesUser, "sales-user-2");
            Assert.False(canAccessOther);
        }

        [Fact]
        public void Scenario10_Manager_HasTeamWideAccess()
        {
            var securityService = new CrmSecurityService();
            var manager = CreateClaimsPrincipal("manager-1", "manager@acxiomcrm.com", "Manager");

            // Manager can access any record in team
            Assert.True(securityService.IsManager(manager));
            Assert.True(securityService.CanAccessRecord(manager, "sales-user-1"));
            Assert.True(securityService.CanAccessRecord(manager, "sales-user-2"));
        }

        [Fact]
        public void Scenario11_Admin_HasFullSystemAccess()
        {
            var securityService = new CrmSecurityService();
            var admin = CreateClaimsPrincipal("admin-1", "admin@acxiomcrm.com", "Admin");

            Assert.True(securityService.IsAdmin(admin));
            Assert.True(securityService.CanAccessRecord(admin, "any-user-record"));
        }

        [Fact]
        public void Scenario12_AuditLogModel_CapturesAllRequiredFields()
        {
            var log = new AuditLog
            {
                UserId = "user-123",
                UserEmail = "sales@acxiomcrm.com",
                Action = "Create",
                EntityName = "Customer",
                RecordId = "CUST-1001",
                OldValue = null,
                NewValue = "Apex Logistics",
                Result = "Success",
                Details = "Created new customer record",
                CreatedDate = DateTime.UtcNow,
                IpAddress = "192.168.1.50"
            };

            Assert.Equal("Create", log.Action);
            Assert.Equal("Customer", log.EntityName);
            Assert.Equal("CUST-1001", log.RecordId);
            Assert.Equal("Success", log.Result);
            Assert.NotNull(log.IpAddress);
        }
    }
}
