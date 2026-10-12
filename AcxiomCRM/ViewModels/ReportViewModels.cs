using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels
{
    public class ReportIndexViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public int ConversionRatePercent { get; set; }
        public int PendingFollowUps { get; set; }
    }

    public class PipelineReportViewModel
    {
        public List<StageSummaryItem> StageSummaries { get; set; } = new();
        public List<OwnerSummaryItem> OwnerSummaries { get; set; } = new();
        public decimal GrandTotalPipeline { get; set; }
        public decimal GrandTotalWeighted { get; set; }
    }

    public class StageSummaryItem
    {
        public string Stage { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class OwnerSummaryItem
    {
        public string OwnerName { get; set; } = string.Empty;
        public int OpenCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
        public int WonCount { get; set; }
        public decimal WonAmount { get; set; }
    }

    public class ConversionReportViewModel
    {
        public int TotalLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public int LostLeads { get; set; }
        public decimal ConversionRatePercent { get; set; }
        public int TotalOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal WinRatePercent { get; set; }
    }

    public class UserActivityReportViewModel
    {
        public List<UserActivitySummaryItem> UserActivities { get; set; } = new();
    }

    public class UserActivitySummaryItem
    {
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int AssignedCustomers { get; set; }
        public int AssignedLeads { get; set; }
        public int AssignedOpportunities { get; set; }
        public int CompletedFollowUps { get; set; }
        public int TotalAuditActions { get; set; }
    }
}
