using System.Data;
using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

public class CategoryService : ICategoryService
{
    private readonly string _connectionString;

    public CategoryService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string not configured.");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<List<ExpenseCategory>> GetAllCategoriesAsync()
    {
        var categories = new List<ExpenseCategory>();

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("dbo.usp_GetAllCategories", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(new ExpenseCategory
            {
                CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
            });
        }

        return categories;
    }
}
