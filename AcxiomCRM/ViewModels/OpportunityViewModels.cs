using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels
{
    public class OpportunityListViewModel
    {
        public List<Opportunity> Opportunities { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? StageFilter { get; set; }
        public string? StatusFilter { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
        public int PageIndex { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
    }

    public class OpportunityCreateEditViewModel
    {
        public int OpportunityId { get; set; }

        [Required(ErrorMessage = "Opportunity Name is required.")]
        [StringLength(120, ErrorMessage = "Opportunity Name cannot exceed 120 characters.")]
        [Display(Name = "Opportunity Name")]
        public string OpportunityName { get; set; } = string.Empty;

        [Display(Name = "Related Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Related Lead")]
        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Opportunity Amount is required.")]
        [Range(0.01, 100000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        [Display(Name = "Amount ($)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Pipeline Stage is required.")]
        [StringLength(40)]
        public string Stage { get; set; } = "Qualification";

        [Required(ErrorMessage = "Probability is required.")]
        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        [Display(Name = "Win Probability (%)")]
        public int Probability { get; set; } = 20;

        [Required(ErrorMessage = "Expected Close Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Open";

        [Display(Name = "Assigned Sales Executive")]
        public string? AssignedTo { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public IEnumerable<SelectListItem>? Customers { get; set; }
        public IEnumerable<SelectListItem>? Leads { get; set; }
        public IEnumerable<SelectListItem>? SalesUsers { get; set; }
    }

    public class OpportunityPipelineViewModel
    {
        public List<Opportunity> QualificationStage { get; set; } = new();
        public List<Opportunity> ProposalStage { get; set; } = new();
        public List<Opportunity> NegotiationStage { get; set; } = new();
        public List<Opportunity> WonStage { get; set; } = new();
        public List<Opportunity> LostStage { get; set; } = new();

        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
    }

    public class OpportunityDetailsViewModel
    {
        public Opportunity Opportunity { get; set; } = null!;
        public List<FollowUp> FollowUps { get; set; } = new();
        public List<Activity> Activities { get; set; } = new();
    }
}
