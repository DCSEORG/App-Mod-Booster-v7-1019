using System.Data;
using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

public class ExpenseService : IExpenseService
{
    private readonly string _connectionString;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string not configured.");
        _logger = logger;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task<List<Expense>> GetAllExpensesAsync(int? userId = null, int? statusId = null)
    {
        var expenses = new List<Expense>();

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("dbo.usp_GetAllExpenses", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            expenses.Add(MapExpense(reader));
        }

        return expenses;
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("dbo.usp_GetExpenseById", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@ExpenseId", expenseId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapExpense(reader);
        }

        return null;
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("dbo.usp_CreateExpense", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@UserId", request.UserId);
        cmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
        cmd.Parameters.AddWithValue("@AmountMinor", request.AmountMinor);
        cmd.Parameters.AddWithValue("@Currency", request.Currency);
        cmd.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
        cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
        => await UpdateStatusAsync(expenseId, "Submitted", null);

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewedBy)
        => await UpdateStatusAsync(expenseId, "Approved", reviewedBy);

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewedBy)
        => await UpdateStatusAsync(expenseId, "Rejected", reviewedBy);

    private async Task<bool> UpdateStatusAsync(int expenseId, string newStatus, int? reviewedBy)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("dbo.usp_UpdateExpenseStatus", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
        cmd.Parameters.AddWithValue("@NewStatus", newStatus);
        cmd.Parameters.AddWithValue("@ReviewedBy", (object?)reviewedBy ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("dbo.usp_DeleteExpense", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@ExpenseId", expenseId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<ExpenseStatsDto> GetExpenseStatsAsync(int? userId = null)
    {
        var stats = new ExpenseStatsDto();

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new SqlCommand("dbo.usp_GetExpenseStats", conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();

        // First result set: status counts
        while (await reader.ReadAsync())
        {
            stats.StatusCounts.Add(new StatusCount
            {
                StatusName = reader.GetString(0),
                Count = reader.GetInt32(1),
                TotalAmountMinor = reader.GetInt32(2)
            });
        }

        // Second result set: totals
        if (await reader.NextResultAsync() && await reader.ReadAsync())
        {
            stats.TotalExpenses = reader.GetInt32(0);
            stats.TotalAmountMinor = reader.GetInt32(1);
            stats.PendingAmountMinor = reader.GetInt32(2);
        }

        return stats;
    }

    private static Expense MapExpense(SqlDataReader reader) => new()
    {
        ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
        UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
        UserName = reader.GetString(reader.GetOrdinal("UserName")),
        UserEmail = reader.GetString(reader.GetOrdinal("UserEmail")),
        CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
        CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
        StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
        StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
        AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
        Currency = reader.GetString(reader.GetOrdinal("Currency")),
        ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
        ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
        SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
        ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
        ReviewedByName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
        ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
    };
}
