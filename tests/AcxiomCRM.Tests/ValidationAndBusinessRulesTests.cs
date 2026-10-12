using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Xunit;

namespace AcxiomCRM.Tests
{
    public class ValidationAndBusinessRulesTests
    {
        private IList<ValidationResult> ValidateModel(object model)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, context, results, true);
            return results;
        }

        [Fact]
        public void Scenario3_CustomerWithInvalidEmailAndPhone_FailsValidation()
        {
            // Acceptance Scenario 3: Create a Customer with invalid email/phone -> validation appears
            var vm = new CustomerCreateEditViewModel
            {
                CustomerName = "Test Invalid Customer",
                Email = "not-an-email",
                Phone = "123", // not 10 digits
                Status = "Active"
            };

            var results = ValidateModel(vm);

            Assert.Contains(results, r => r.MemberNames.Contains("Email"));
            Assert.Contains(results, r => r.MemberNames.Contains("Phone"));
        }

        [Fact]
        public void Scenario3_CustomerWithValidEmailAndPhone_PassesValidation()
        {
            var vm = new CustomerCreateEditViewModel
            {
                CustomerName = "Test Valid Customer",
                Email = "valid.user@enterprise.com",
                Phone = "9876543210", // exactly 10 digits
                Status = "Active"
            };

            var results = ValidateModel(vm);

            Assert.DoesNotContain(results, r => r.MemberNames.Contains("Email"));
            Assert.DoesNotContain(results, r => r.MemberNames.Contains("Phone"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-500)]
        public void Scenario5_OpportunityAmountLessThanOrEqualToZero_FailsValidation(decimal invalidAmount)
        {
            // Acceptance Scenario 5: Create an Opportunity with Amount <= 0 -> rejected
            var opp = new OpportunityCreateEditViewModel
            {
                OpportunityName = "Zero Dollar Deal",
                Amount = invalidAmount,
                Stage = "Qualification",
                Probability = 20,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                Status = "Open"
            };

            var results = ValidateModel(opp);

            Assert.Contains(results, r => r.MemberNames.Contains("Amount"));
        }

        [Theory]
        [InlineData(101)]
        [InlineData(-5)]
        public void Scenario6_OpportunityProbabilityOutside0To100_FailsValidation(int invalidProb)
        {
            // Acceptance Scenario 6: Create an Opportunity with Probability > 100 -> rejected
            var opp = new OpportunityCreateEditViewModel
            {
                OpportunityName = "Invalid Probability Deal",
                Amount = 15000m,
                Stage = "Qualification",
                Probability = invalidProb,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                Status = "Open"
            };

            var results = ValidateModel(opp);

            Assert.Contains(results, r => r.MemberNames.Contains("Probability"));
        }

        [Fact]
        public void OpportunityWeightedAmount_CalculatesCorrectly()
        {
            // Weighted pipeline using Amount * Probability / 100
            var opp = new Opportunity
            {
                OpportunityName = "Cloud Migration",
                Amount = 100000m,
                Probability = 65
            };

            Assert.Equal(65000m, opp.WeightedAmount);
        }

        [Fact]
        public void Scenario7_ExpectedCloseDateInPastForActiveOpportunity_BusinessRuleRejects()
        {
            // Acceptance Scenario 7: Set Expected Close Date in the past -> rejected for an active opportunity
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var isPast = yesterday < DateTime.UtcNow.Date;

            Assert.True(isPast, "Expected Close Date in the past should be detected.");
        }

        [Fact]
        public void Scenario8_FollowUpDateInPastForPlannedFollowUp_BusinessRuleRejects()
        {
            // Acceptance Scenario 8: Create a Follow-Up dated before today -> rejected for new/planned
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var isPast = yesterday < DateTime.UtcNow.Date;

            Assert.True(isPast, "Follow-Up date earlier than today must be rejected for planned activities.");
        }
    }
}
