using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;
using ApparelERP.Api.Services;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN,EMPLOYEE,CA")]
    public class SalesController : ControllerBase
    {
        private readonly ISalesService _salesService;

        public SalesController(ISalesService salesService)
        {
            _salesService = salesService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SalesOrder>>> GetSalesOrders()
        {
            var salesOrders = await _salesService.GetAllSalesOrdersAsync();
            return Ok(salesOrders);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalesOrder>> GetSalesOrder(int id)
        {
            var salesOrder = await _salesService.GetSalesOrderByIdAsync(id);
            if (salesOrder == null)
            {
                return NotFound();
            }
            return Ok(salesOrder);
        }

        [HttpPost]
        public async Task<ActionResult<SalesOrder>> CreateSalesOrder(CreateSalesOrderDto dto)
        {
            try
            {
                var salesOrder = await _salesService.CreateSalesOrderAsync(dto);
                return CreatedAtAction(nameof(GetSalesOrder), new { id = salesOrder.Id }, salesOrder);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSalesOrder(int id)
        {
            try
            {
                await _salesService.DeleteSalesOrderAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
