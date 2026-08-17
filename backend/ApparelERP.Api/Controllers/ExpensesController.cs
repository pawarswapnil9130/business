using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Models.Dto;

namespace ApparelERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN,EMPLOYEE,CA")]
    public class ExpensesController : ControllerBase
    {
        private readonly ApparelDbContext _context;

        public ExpensesController(ApparelDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExpenseSummaryDto>>> GetExpenses()
        {
            var expenses = await _context.Expenses
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.Id)
                .Select(e => new ExpenseSummaryDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    Category = e.Category,
                    Amount = e.Amount,
                    ExpenseDate = e.ExpenseDate,
                    PaymentMode = e.PaymentMode,
                    Notes = e.Notes,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            return Ok(expenses);
        }

        [HttpPost]
        public async Task<ActionResult<ExpenseSummaryDto>> CreateExpense([FromBody] CreateExpenseDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(new { message = "Expense title or description is required." });
            }

            if (dto.Amount <= 0)
            {
                return BadRequest(new { message = "Expense amount must be greater than zero." });
            }

            var expense = new Expense
            {
                Title = dto.Title.Trim(),
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
                Amount = dto.Amount,
                ExpenseDate = dto.ExpenseDate.HasValue ? DateTime.SpecifyKind(dto.ExpenseDate.Value, DateTimeKind.Utc) : DateTime.UtcNow,
                PaymentMode = string.IsNullOrWhiteSpace(dto.PaymentMode) ? "CASH" : dto.PaymentMode.Trim().ToUpper(),
                Notes = dto.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            var result = new ExpenseSummaryDto
            {
                Id = expense.Id,
                Title = expense.Title,
                Category = expense.Category,
                Amount = expense.Amount,
                ExpenseDate = expense.ExpenseDate,
                PaymentMode = expense.PaymentMode,
                Notes = expense.Notes,
                CreatedAt = expense.CreatedAt
            };

            return CreatedAtAction(nameof(GetExpenses), new { id = expense.Id }, result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null)
            {
                return NotFound(new { message = "Expense not found." });
            }

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Expense deleted successfully." });
        }
    }
}
