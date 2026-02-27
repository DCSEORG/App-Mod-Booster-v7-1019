using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>Get all expenses, optionally filtered by userId or statusId</summary>
    [HttpGet]
    public async Task<ActionResult<List<Expense>>> GetAll([FromQuery] int? userId, [FromQuery] int? statusId)
    {
        try
        {
            var expenses = await _expenseService.GetAllExpensesAsync(userId, statusId);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get expense by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Expense>> GetById(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null) return NotFound(new { error = $"Expense {id} not found" });
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Create a new expense</summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateExpenseRequest request)
    {
        try
        {
            var newId = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = newId }, new { expenseId = newId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Submit an expense for approval</summary>
    [HttpPut("{id:int}/submit")]
    public async Task<ActionResult> Submit(int id)
    {
        try
        {
            var result = await _expenseService.SubmitExpenseAsync(id);
            if (!result) return NotFound(new { error = $"Expense {id} not found" });
            return Ok(new { success = true, message = $"Expense {id} submitted for approval" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Approve an expense</summary>
    [HttpPut("{id:int}/approve")]
    public async Task<ActionResult> Approve(int id, [FromBody] ApproveExpenseRequest request)
    {
        try
        {
            var result = await _expenseService.ApproveExpenseAsync(id, request.ReviewedBy);
            if (!result) return NotFound(new { error = $"Expense {id} not found" });
            return Ok(new { success = true, message = $"Expense {id} approved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Reject an expense</summary>
    [HttpPut("{id:int}/reject")]
    public async Task<ActionResult> Reject(int id, [FromBody] ApproveExpenseRequest request)
    {
        try
        {
            var result = await _expenseService.RejectExpenseAsync(id, request.ReviewedBy);
            if (!result) return NotFound(new { error = $"Expense {id} not found" });
            return Ok(new { success = true, message = $"Expense {id} rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Delete an expense</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var result = await _expenseService.DeleteExpenseAsync(id);
            if (!result) return NotFound(new { error = $"Expense {id} not found" });
            return Ok(new { success = true, message = $"Expense {id} deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get expense statistics for the dashboard</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ExpenseStatsDto>> GetStats([FromQuery] int? userId)
    {
        try
        {
            var stats = await _expenseService.GetExpenseStatsAsync(userId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense stats");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
