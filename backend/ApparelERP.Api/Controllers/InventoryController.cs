using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ApparelERP.Api.Models.Dto;
using ApparelERP.Api.Services;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<IEnumerable<StockSummaryDto>>> GetStockSummary()
        {
            var summary = await _inventoryService.GetStockSummaryAsync();
            return Ok(summary);
        }

        [HttpDelete("clear")]
        [Authorize(Roles = "SUPER_ADMIN,ADMIN")]
        public async Task<IActionResult> ClearAllStock()
        {
            await _inventoryService.ClearAllStockAsync();
            return Ok(new { message = "All stock ledger entries cleared successfully." });
        }
    }
}
