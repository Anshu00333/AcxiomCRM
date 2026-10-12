using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels
{
    public class FollowUpListViewModel
    {
        public List<FollowUp> FollowUps { get; set; } = new();
        public string? FilterType { get; set; } = "All"; // All, Planned, Today, Overdue, Completed
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public int PageIndex { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
    }

    public class FollowUpCreateEditViewModel
    {
        public int FollowUpId { get; set; }

        [Display(Name = "Related Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Related Lead")]
        public int? LeadId { get; set; }

        [StringLength(30)]
        public string? RelatedType { get; set; } = "Lead";

        [Required(ErrorMessage = "Follow-up Date is required.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Scheduled Date & Time")]
        public DateTime FollowUpDate { get; set; } = DateTime.UtcNow.AddDays(1);

        [Required(ErrorMessage = "Follow-up Type is required.")]
        [StringLength(30)]
        [Display(Name = "Activity Type")]
        public string FollowUpType { get; set; } = "Call";

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Notes / Agenda")]
        public string? Remarks { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Planned";

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        public IEnumerable<SelectListItem>? Customers { get; set; }
        public IEnumerable<SelectListItem>? Leads { get; set; }
        public IEnumerable<SelectListItem>? SalesUsers { get; set; }
    }

    public class CompleteFollowUpViewModel
    {
        public int FollowUpId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? Remarks { get; set; }

        [Required(ErrorMessage = "Outcome notes are required to complete.")]
        [StringLength(1000)]
        [Display(Name = "Outcome / Meeting Notes")]
        public string OutcomeNotes { get; set; } = string.Empty;

        [Display(Name = "Schedule Next Follow-Up")]
        public bool ScheduleNext { get; set; }

        [Display(Name = "Next Follow-Up Date")]
        public DateTime? NextFollowUpDate { get; set; }

        [Display(Name = "Next Follow-Up Type")]
        public string? NextFollowUpType { get; set; } = "Call";

        [Display(Name = "Next Subject")]
        public string? NextSubject { get; set; }
    }
}
