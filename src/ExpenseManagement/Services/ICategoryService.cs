using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface ICategoryService
{
    Task<List<ExpenseCategory>> GetAllCategoriesAsync();
}
