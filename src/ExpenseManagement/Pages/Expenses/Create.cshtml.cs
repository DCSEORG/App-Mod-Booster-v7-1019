using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ExpenseManagement.Pages.Expenses;

public class CreateModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ICategoryService _categoryService;
    private readonly IUserService _userService;
    private readonly ILogger<CreateModel> _logger;

    [BindProperty]
    public CreateExpenseRequest NewExpense { get; set; } = new() { ExpenseDate = DateTime.Today, Currency = "GBP" };

    public List<SelectListItem> Categories { get; set; } = new();
    public List<SelectListItem> Users { get; set; } = new();

    public CreateModel(IExpenseService expenseService, ICategoryService categoryService,
        IUserService userService, ILogger<CreateModel> logger)
    {
        _expenseService = expenseService;
        _categoryService = categoryService;
        _userService = userService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        await LoadDropdownsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return Page();
        }

        try
        {
            var newId = await _expenseService.CreateExpenseAsync(NewExpense);
            TempData["SuccessMessage"] = $"Expense #{newId} created successfully.";
            return RedirectToPage("/Expenses/Details", new { id = newId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create expense");
            ModelState.AddModelError(string.Empty, $"Failed to create expense: {ex.Message}");
            await LoadDropdownsAsync();
            return Page();
        }
    }

    private async Task LoadDropdownsAsync()
    {
        try
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            Categories = categories.Select(c => new SelectListItem(c.CategoryName, c.CategoryId.ToString())).ToList();

            var users = await _userService.GetAllUsersAsync();
            Users = users.Select(u => new SelectListItem(u.UserName, u.UserId.ToString())).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dropdowns");
            SetDbError(ex);
            Categories = new List<SelectListItem>
            {
                new("Travel", "1"), new("Meals", "2"), new("Supplies", "3"),
                new("Accommodation", "4"), new("Other", "5")
            };
            Users = new List<SelectListItem>
            {
                new("Alice Example", "1"), new("Bob Manager", "2")
            };
        }
    }

    private void SetDbError(Exception ex)
    {
        ViewData["DbError"] = $"Could not connect to the database: {ex.GetType().Name}";
        ViewData["DbErrorDetail"] = ex.Message;
    }
}
