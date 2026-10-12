using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmSecurityService _securityService;

        public ReportsApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmSecurityService securityService)
        {
            _context = context;
            _userManager = userManager;
            _securityService = securityService;
        }

        [HttpGet("pipeline")]
        public async Task<ActionResult<PipelineReportDto>> GetPipelineReport()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _context.Opportunities.AsQueryable();

            if (_securityService.IsSalesExecutive(User))
            {
                query = query.Where(o => o.AssignedTo == user.Id);
            }

            var opps = await query.ToListAsync();

            var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
            var stageSummaries = stages.Select(s =>
            {
                var stageOpps = opps.Where(o => o.Stage == s).ToList();
                return new StageSummaryDto
                {
                    Stage = s,
                    Count = stageOpps.Count,
                    TotalAmount = stageOpps.Sum(o => o.Amount),
                    WeightedAmount = stageOpps.Sum(o => o.WeightedAmount)
                };
            }).ToList();

            var dto = new PipelineReportDto
            {
                TotalPipelineValue = opps.Where(o => o.Status == "Open").Sum(o => o.Amount),
                WeightedPipelineValue = opps.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount),
                TotalOpportunitiesCount = opps.Count,
                StageSummaries = stageSummaries
            };

            return Ok(dto);
        }
    }
}
