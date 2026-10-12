using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await context.Database.EnsureCreatedAsync();

            // 1. Roles
            string[] roles = { "Admin", "Manager", "SalesExecutive" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Default Users
            var adminUser = await userManager.FindByEmailAsync("admin@acxiomcrm.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@acxiomcrm.com",
                    Email = "admin@acxiomcrm.com",
                    FullName = "System Administrator",
                    Department = "IT Operations",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            var managerUser = await userManager.FindByEmailAsync("manager@acxiomcrm.com");
            if (managerUser == null)
            {
                managerUser = new ApplicationUser
                {
                    UserName = "manager@acxiomcrm.com",
                    Email = "manager@acxiomcrm.com",
                    FullName = "Sarah Jenkins",
                    Department = "Sales Management",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(managerUser, "Manager@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(managerUser, "Manager");
                }
            }

            var salesUser1 = await userManager.FindByEmailAsync("sales@acxiomcrm.com");
            if (salesUser1 == null)
            {
                salesUser1 = new ApplicationUser
                {
                    UserName = "sales@acxiomcrm.com",
                    Email = "sales@acxiomcrm.com",
                    FullName = "David Miller",
                    Department = "Direct Sales",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(salesUser1, "Sales@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(salesUser1, "SalesExecutive");
                }
            }

            var salesUser2 = await userManager.FindByEmailAsync("sales2@acxiomcrm.com");
            if (salesUser2 == null)
            {
                salesUser2 = new ApplicationUser
                {
                    UserName = "sales2@acxiomcrm.com",
                    Email = "sales2@acxiomcrm.com",
                    FullName = "Emily Watson",
                    Department = "Enterprise Accounts",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(salesUser2, "Sales@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(salesUser2, "SalesExecutive");
                }
            }

            // 3. Customers
            if (!context.Customers.Any())
            {
                var customers = new List<Customer>
                {
                    new Customer
                    {
                        CustomerCode = "CUST-1001",
                        CustomerName = "Apex Logistics Ltd",
                        Email = "james@apexlogistics.com",
                        Phone = "9876543210",
                        CompanyName = "Apex Global Logistics",
                        Address = "45 Commercial Boulevard, Suite 300",
                        City = "Chicago",
                        State = "IL",
                        Status = "Active",
                        CreatedBy = "System",
                        AssignedTo = salesUser1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-60)
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1002",
                        CustomerName = "Nexus Health Systems",
                        Email = "elena@nexushealth.com",
                        Phone = "9876543211",
                        CompanyName = "Nexus Health Corp",
                        Address = "1200 Medical Center Way",
                        City = "Boston",
                        State = "MA",
                        Status = "Active",
                        CreatedBy = "System",
                        AssignedTo = salesUser1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-45)
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1003",
                        CustomerName = "Starlight Financial Group",
                        Email = "robert@starlightfin.com",
                        Phone = "9876543212",
                        CompanyName = "Starlight Capital",
                        Address = "700 Wall Street Plaza",
                        City = "New York",
                        State = "NY",
                        Status = "Active",
                        CreatedBy = "System",
                        AssignedTo = salesUser2.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-30)
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1004",
                        CustomerName = "Vanguard Industrial",
                        Email = "thomas@vanguardind.com",
                        Phone = "9876543213",
                        CompanyName = "Vanguard Heavy Industries",
                        Address = "88 Manufacturing Road",
                        City = "Detroit",
                        State = "MI",
                        Status = "Prospect",
                        CreatedBy = "System",
                        AssignedTo = salesUser2.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-20)
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1005",
                        CustomerName = "CloudScale Technologies",
                        Email = "sophia@cloudscale.io",
                        Phone = "9876543214",
                        CompanyName = "CloudScale Inc",
                        Address = "500 Innovation Way",
                        City = "San Jose",
                        State = "CA",
                        Status = "Active",
                        CreatedBy = "System",
                        AssignedTo = salesUser1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-15)
                    }
                };
                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();
            }

            // 4. Leads
            if (!context.Leads.Any())
            {
                var leads = new List<Lead>
                {
                    new Lead
                    {
                        LeadCode = "LEAD-2001",
                        LeadName = "Horizon Energy Corp",
                        Email = "michael@horizonenergy.com",
                        Phone = "9811223344",
                        CompanyName = "Horizon Energy Partners",
                        Source = "Website",
                        Status = "New",
                        Priority = "High",
                        ExpectedValue = 45000.00m,
                        AssignedTo = salesUser1.Id,
                        Notes = "Inquired via landing page contact form regarding enterprise tier.",
                        CreatedDate = DateTime.UtcNow.AddDays(-10)
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-2002",
                        LeadName = "BluePeak Analytics",
                        Email = "olivia@bluepeak.ai",
                        Phone = "9822334455",
                        CompanyName = "BluePeak AI Labs",
                        Source = "Referral",
                        Status = "Contacted",
                        Priority = "Medium",
                        ExpectedValue = 72000.00m,
                        AssignedTo = salesUser1.Id,
                        Notes = "Referred by James at Apex Logistics. Initial discovery call conducted.",
                        CreatedDate = DateTime.UtcNow.AddDays(-7)
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-2003",
                        LeadName = "Titan Infrastructure",
                        Email = "marcus@titaninfra.com",
                        Phone = "9833445566",
                        CompanyName = "Titan Buildworks",
                        Source = "Trade Show",
                        Status = "Qualified",
                        Priority = "High",
                        ExpectedValue = 120000.00m,
                        AssignedTo = salesUser2.Id,
                        Notes = "Met at Global Tech Summit. Ready for deep-dive demonstration.",
                        CreatedDate = DateTime.UtcNow.AddDays(-5)
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-2004",
                        LeadName = "Silverline Retail",
                        Email = "chloe@silverline.com",
                        Phone = "9844556677",
                        CompanyName = "Silverline Stores",
                        Source = "Cold Call",
                        Status = "Lost",
                        Priority = "Low",
                        ExpectedValue = 18000.00m,
                        AssignedTo = salesUser2.Id,
                        Notes = "Current budget frozen until Q3 next year.",
                        CreatedDate = DateTime.UtcNow.AddDays(-12)
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-2005",
                        LeadName = "Zenith Robotics",
                        Email = "daniel@zenithrobotics.com",
                        Phone = "9855667788",
                        CompanyName = "Zenith Automation",
                        Source = "Website",
                        Status = "Converted",
                        Priority = "High",
                        ExpectedValue = 95000.00m,
                        AssignedTo = salesUser1.Id,
                        Notes = "Converted to enterprise opportunity.",
                        CreatedDate = DateTime.UtcNow.AddDays(-25)
                    }
                };
                context.Leads.AddRange(leads);
                await context.SaveChangesAsync();
            }

            // 5. Opportunities
            if (!context.Opportunities.Any())
            {
                var cust1 = context.Customers.FirstOrDefault(c => c.CustomerCode == "CUST-1001");
                var cust2 = context.Customers.FirstOrDefault(c => c.CustomerCode == "CUST-1002");
                var cust3 = context.Customers.FirstOrDefault(c => c.CustomerCode == "CUST-1003");
                var cust4 = context.Customers.FirstOrDefault(c => c.CustomerCode == "CUST-1004");
                var cust5 = context.Customers.FirstOrDefault(c => c.CustomerCode == "CUST-1005");

                var opportunities = new List<Opportunity>
                {
                    new Opportunity
                    {
                        OpportunityName = "Enterprise Cloud Migration",
                        CustomerId = cust5?.CustomerId,
                        Amount = 85000.00m,
                        Stage = "Proposal",
                        Probability = 60,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                        Status = "Open",
                        AssignedTo = salesUser1.Id,
                        Notes = "Custom cloud setup with SLA tier 1.",
                        CreatedDate = DateTime.UtcNow.AddDays(-12)
                    },
                    new Opportunity
                    {
                        OpportunityName = "Fleet Tracking Modernization",
                        CustomerId = cust1?.CustomerId,
                        Amount = 120000.00m,
                        Stage = "Negotiation",
                        Probability = 80,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
                        Status = "Open",
                        AssignedTo = salesUser1.Id,
                        Notes = "Contract finalization in progress with legal department.",
                        CreatedDate = DateTime.UtcNow.AddDays(-20)
                    },
                    new Opportunity
                    {
                        OpportunityName = "Secure Compliance Suite",
                        CustomerId = cust3?.CustomerId,
                        Amount = 65000.00m,
                        Stage = "Won",
                        Probability = 100,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(-5),
                        Status = "Won",
                        AssignedTo = salesUser2.Id,
                        Notes = "Annual agreement signed.",
                        CreatedDate = DateTime.UtcNow.AddDays(-30)
                    },
                    new Opportunity
                    {
                        OpportunityName = "Smart Sensor Mesh",
                        CustomerId = cust4?.CustomerId,
                        Amount = 42000.00m,
                        Stage = "Qualification",
                        Probability = 30,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(40),
                        Status = "Open",
                        AssignedTo = salesUser2.Id,
                        Notes = "Pilot project assessment underway.",
                        CreatedDate = DateTime.UtcNow.AddDays(-4)
                    },
                    new Opportunity
                    {
                        OpportunityName = "Healthcare Records Platform",
                        CustomerId = cust2?.CustomerId,
                        Amount = 150000.00m,
                        Stage = "Negotiation",
                        Probability = 75,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(18),
                        Status = "Open",
                        AssignedTo = salesUser1.Id,
                        Notes = "HIPAA compliance audit passed; awaiting board approval.",
                        CreatedDate = DateTime.UtcNow.AddDays(-18)
                    }
                };
                context.Opportunities.AddRange(opportunities);
                await context.SaveChangesAsync();
            }

            // 6. Follow-ups
            if (!context.FollowUps.Any())
            {
                var cust1 = context.Customers.FirstOrDefault(c => c.CustomerCode == "CUST-1001");
                var lead1 = context.Leads.FirstOrDefault(l => l.LeadCode == "LEAD-2001");

                var followUps = new List<FollowUp>
                {
                    new FollowUp
                    {
                        CustomerId = cust1?.CustomerId,
                        RelatedType = "Customer",
                        RelatedId = cust1?.CustomerId,
                        FollowUpDate = DateTime.UtcNow.AddDays(2),
                        FollowUpType = "Meeting",
                        Subject = "Executive Demo & Pricing Review",
                        Remarks = "Present executive summary and answer stakeholder questions.",
                        Status = "Planned",
                        AssignedTo = salesUser1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-1)
                    },
                    new FollowUp
                    {
                        LeadId = lead1?.LeadId,
                        RelatedType = "Lead",
                        RelatedId = lead1?.LeadId,
                        FollowUpDate = DateTime.UtcNow.AddDays(1),
                        FollowUpType = "Call",
                        Subject = "Initial Qualification Call",
                        Remarks = "Discuss functional requirements and procurement timeline.",
                        Status = "Planned",
                        AssignedTo = salesUser1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-2)
                    },
                    new FollowUp
                    {
                        CustomerId = cust1?.CustomerId,
                        RelatedType = "Customer",
                        RelatedId = cust1?.CustomerId,
                        FollowUpDate = DateTime.UtcNow.AddDays(-3),
                        FollowUpType = "Email",
                        Subject = "Sent Security Architecture Whitepaper",
                        Remarks = "Shared documentation requested during Tuesday call.",
                        Status = "Completed",
                        CompletedDate = DateTime.UtcNow.AddDays(-3),
                        AssignedTo = salesUser1.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-5)
                    }
                };
                context.FollowUps.AddRange(followUps);
                await context.SaveChangesAsync();
            }

            // 7. Activities
            if (!context.Activities.Any())
            {
                var cust1 = context.Customers.FirstOrDefault(c => c.CustomerCode == "CUST-1001");
                var activities = new List<Activity>
                {
                    new Activity
                    {
                        ActivityType = "Meeting",
                        Subject = "Quarterly Business Review",
                        Description = "Met with procurement head to review platform performance and roadmaps.",
                        ActivityDate = DateTime.UtcNow.AddDays(-10),
                        CustomerId = cust1?.CustomerId,
                        AssignedTo = salesUser1.Id,
                        Status = "Completed"
                    },
                    new Activity
                    {
                        ActivityType = "Call",
                        Subject = "Contract Negotiations",
                        Description = "Clarified clause 4.2 in the master services agreement.",
                        ActivityDate = DateTime.UtcNow.AddDays(-4),
                        CustomerId = cust1?.CustomerId,
                        AssignedTo = salesUser1.Id,
                        Status = "Completed"
                    }
                };
                context.Activities.AddRange(activities);
                await context.SaveChangesAsync();
            }

            // 8. Seed Audit Log entries
            if (!context.AuditLogs.Any())
            {
                var auditLogs = new List<AuditLog>
                {
                    new AuditLog
                    {
                        UserId = adminUser.Id,
                        UserEmail = adminUser.Email,
                        Action = "Security",
                        EntityName = "Auth",
                        RecordId = adminUser.Id,
                        Result = "Success",
                        Details = "System roles and security baseline initialized.",
                        CreatedDate = DateTime.UtcNow.AddDays(-60),
                        IpAddress = "127.0.0.1"
                    },
                    new AuditLog
                    {
                        UserId = adminUser.Id,
                        UserEmail = adminUser.Email,
                        Action = "Create",
                        EntityName = "Customer",
                        RecordId = "CUST-1001",
                        Result = "Success",
                        Details = "Created customer Apex Logistics Ltd.",
                        CreatedDate = DateTime.UtcNow.AddDays(-60),
                        IpAddress = "127.0.0.1"
                    }
                };
                context.AuditLogs.AddRange(auditLogs);
                await context.SaveChangesAsync();
            }
        }
    }
}
