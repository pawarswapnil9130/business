using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApparelERP.Api.Models.Dto;
using ApparelERP.Api.Services;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN,EMPLOYEE,CA")]
    public class ReportsController : ControllerBase
    {
        private readonly ISalesService _salesService;

        public ReportsController(ISalesService salesService)
        {
            _salesService = salesService;
        }

        [HttpGet("profit")]
        public async Task<ActionResult<IEnumerable<ProfitReportDto>>> GetProfitReport()
        {
            var report = await _salesService.GetProfitReportAsync();
            return Ok(report);
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<DashboardSummaryDto>> GetDashboardSummary()
        {
            var summary = await _salesService.GetDashboardSummaryAsync();
            return Ok(summary);
        }
    }
}
