using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Expenses;

public class ExpensesIndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesIndexModel> _logger;

    public List<Expense> Expenses { get; set; } = new();
    public string? FilterStatus { get; set; }

    public ExpensesIndexModel(IExpenseService expenseService, ILogger<ExpensesIndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync(string? status = null)
    {
        FilterStatus = status;
        try
        {
            int? statusId = status switch
            {
                "draft" => 1,
                "submitted" => 2,
                "approved" => 3,
                "rejected" => 4,
                _ => null
            };
            Expenses = await _expenseService.GetAllExpensesAsync(statusId: statusId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load expenses");
            SetDbError(ex);
            Expenses = GetDummyExpenses();
        }
    }

    private void SetDbError(Exception ex)
    {
        ViewData["DbError"] = $"Could not connect to the database: {ex.GetType().Name}";
        ViewData["DbErrorDetail"] = $"{ex.Message}";
    }

    private static List<Expense> GetDummyExpenses() => new()
    {
        new() { ExpenseId=1, UserName="Alice Example", CategoryName="Travel", StatusName="Submitted", AmountMinor=2540, ExpenseDate=new DateTime(2025,10,20), Description="Taxi from airport to client site" },
        new() { ExpenseId=2, UserName="Alice Example", CategoryName="Meals", StatusName="Approved", AmountMinor=1425, ExpenseDate=new DateTime(2025,9,15), Description="Client lunch meeting" },
        new() { ExpenseId=3, UserName="Alice Example", CategoryName="Supplies", StatusName="Draft", AmountMinor=799, ExpenseDate=new DateTime(2025,11,1), Description="Office stationery" },
        new() { ExpenseId=4, UserName="Alice Example", CategoryName="Accommodation", StatusName="Approved", AmountMinor=12300, ExpenseDate=new DateTime(2025,8,10), Description="Hotel during client visit" }
    };
}
