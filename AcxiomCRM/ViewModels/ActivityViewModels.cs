using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels
{
    public class ActivityListViewModel
    {
        public List<Activity> Activities { get; set; } = new();
        public string? TypeFilter { get; set; }
        public string? SearchTerm { get; set; }
        public int PageIndex { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
    }

    public class ActivityCreateEditViewModel
    {
        public int ActivityId { get; set; }

        [Required(ErrorMessage = "Activity Type is required.")]
        [StringLength(30)]
        [Display(Name = "Activity Type")]
        public string ActivityType { get; set; } = "Call"; // Call, Meeting, Email, Task

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Description / Summary")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Activity Date is required.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Activity Date & Time")]
        public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Related Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Related Lead")]
        public int? LeadId { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Completed";

        public IEnumerable<SelectListItem>? Customers { get; set; }
        public IEnumerable<SelectListItem>? Leads { get; set; }
        public IEnumerable<SelectListItem>? SalesUsers { get; set; }
    }
}
