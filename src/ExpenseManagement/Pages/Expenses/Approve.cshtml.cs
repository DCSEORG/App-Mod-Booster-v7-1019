using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Expenses;

public class ApproveModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ApproveModel> _logger;

    public List<Expense> PendingExpenses { get; set; } = new();
    public Expense? SelectedExpense { get; set; }

    public ApproveModel(IExpenseService expenseService, ILogger<ApproveModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync(int? id = null)
    {
        try
        {
            PendingExpenses = await _expenseService.GetAllExpensesAsync(statusId: 2); // Submitted
            if (id.HasValue)
            {
                SelectedExpense = PendingExpenses.FirstOrDefault(e => e.ExpenseId == id.Value)
                    ?? await _expenseService.GetExpenseByIdAsync(id.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending expenses");
            SetDbError(ex);
            PendingExpenses = new List<Expense>
            {
                new() { ExpenseId=1, UserName="Alice Example", CategoryName="Travel",
                    StatusName="Submitted", AmountMinor=2540,
                    ExpenseDate=new DateTime(2025,10,20), Description="Taxi from airport" }
            };
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId, int reviewedBy = 2)
    {
        try
        {
            await _expenseService.ApproveExpenseAsync(expenseId, reviewedBy);
            TempData["SuccessMessage"] = $"Expense #{expenseId} approved.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to approve: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId, int reviewedBy = 2)
    {
        try
        {
            await _expenseService.RejectExpenseAsync(expenseId, reviewedBy);
            TempData["SuccessMessage"] = $"Expense #{expenseId} rejected.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Failed to reject: {ex.Message}";
        }
        return RedirectToPage();
    }

    private void SetDbError(Exception ex)
    {
        ViewData["DbError"] = $"Could not connect to the database: {ex.GetType().Name}";
        ViewData["DbErrorDetail"] = ex.Message;
    }
}
