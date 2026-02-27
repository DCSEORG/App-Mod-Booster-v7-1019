using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Expenses;

public class DetailsModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<DetailsModel> _logger;

    public Expense? Expense { get; set; }

    public DetailsModel(IExpenseService expenseService, ILogger<DetailsModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Expense = await _expenseService.GetExpenseByIdAsync(id);
            if (Expense == null)
            {
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load expense {Id}", id);
            SetDbError(ex);
            Expense = new Expense
            {
                ExpenseId = id, UserName = "Alice Example", CategoryName = "Travel",
                StatusName = "Submitted", AmountMinor = 2540,
                ExpenseDate = DateTime.Today, Description = "Demo expense (DB unavailable)"
            };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        try
        {
            await _expenseService.SubmitExpenseAsync(id);
            TempData["SuccessMessage"] = $"Expense #{id} submitted for approval.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to submit: {ex.Message}";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
            TempData["SuccessMessage"] = $"Expense #{id} deleted.";
            return RedirectToPage("/Expenses/Index");
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to delete: {ex.Message}";
            return RedirectToPage(new { id });
        }
    }

    private void SetDbError(Exception ex)
    {
        ViewData["DbError"] = $"Could not connect to the database: {ex.GetType().Name}";
        ViewData["DbErrorDetail"] = ex.Message;
    }
}
