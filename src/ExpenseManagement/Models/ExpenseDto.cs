namespace ExpenseManagement.Models;

public class ExpenseDto
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int AmountMinor { get; set; }
    public decimal AmountDecimal => AmountMinor / 100m;
    public string AmountFormatted => $"£{AmountDecimal:F2}";
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExpenseRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }

    /// <summary>Amount in pence (e.g. £25.40 = 2540)</summary>
    public int AmountMinor { get; set; }

    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
}

public class ApproveExpenseRequest
{
    public int ReviewedBy { get; set; }
    public string? Notes { get; set; }
}

public class ExpenseStatsDto
{
    public List<StatusCount> StatusCounts { get; set; } = new();
    public int TotalExpenses { get; set; }
    public int TotalAmountMinor { get; set; }
    public decimal TotalAmountDecimal => TotalAmountMinor / 100m;
    public int PendingAmountMinor { get; set; }
    public decimal PendingAmountDecimal => PendingAmountMinor / 100m;
}

public class StatusCount
{
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public int TotalAmountMinor { get; set; }
    public decimal TotalAmountDecimal => TotalAmountMinor / 100m;
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto>? History { get; set; }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
