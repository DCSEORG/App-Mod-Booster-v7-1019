using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public ExpenseStatsDto Stats { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();

    public IndexModel(IExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Stats = await _expenseService.GetExpenseStatsAsync();
            var allExpenses = await _expenseService.GetAllExpensesAsync();
            RecentExpenses = allExpenses.Take(5).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard data");
            SetDbError(ex);
            Stats = GetDummyStats();
            RecentExpenses = GetDummyExpenses().Take(5).ToList();
        }
    }

    private void SetDbError(Exception ex)
    {
        var fileName = ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? "unknown";
        ViewData["DbError"] = $"Could not connect to the database: {ex.GetType().Name}";
        ViewData["DbErrorDetail"] = $"{ex.Message} | {fileName}";
    }

    private static ExpenseStatsDto GetDummyStats() => new()
    {
        TotalExpenses = 4,
        TotalAmountMinor = 17064,
        PendingAmountMinor = 2540,
        StatusCounts = new()
        {
            new() { StatusName = "Draft", Count = 1, TotalAmountMinor = 799 },
            new() { StatusName = "Submitted", Count = 1, TotalAmountMinor = 2540 },
            new() { StatusName = "Approved", Count = 2, TotalAmountMinor = 13725 },
            new() { StatusName = "Rejected", Count = 0, TotalAmountMinor = 0 }
        }
    };

    private static List<Expense> GetDummyExpenses() => new()
    {
        new() { ExpenseId=1, UserName="Alice Example", CategoryName="Travel", StatusName="Submitted", AmountMinor=2540, ExpenseDate=new DateTime(2025,10,20), Description="Taxi from airport" },
        new() { ExpenseId=2, UserName="Alice Example", CategoryName="Meals", StatusName="Approved", AmountMinor=1425, ExpenseDate=new DateTime(2025,9,15), Description="Client lunch" },
        new() { ExpenseId=3, UserName="Alice Example", CategoryName="Supplies", StatusName="Draft", AmountMinor=799, ExpenseDate=new DateTime(2025,11,1), Description="Office stationery" },
        new() { ExpenseId=4, UserName="Alice Example", CategoryName="Accommodation", StatusName="Approved", AmountMinor=12300, ExpenseDate=new DateTime(2025,8,10), Description="Hotel - client visit" }
    };
}
