using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetAllExpensesAsync(int? userId = null, int? statusId = null);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<int> CreateExpenseAsync(CreateExpenseRequest request);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewedBy);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewedBy);
    Task<bool> DeleteExpenseAsync(int expenseId);
    Task<ExpenseStatsDto> GetExpenseStatsAsync(int? userId = null);
}
