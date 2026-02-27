using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IUserService
{
    Task<List<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(int userId);
}
