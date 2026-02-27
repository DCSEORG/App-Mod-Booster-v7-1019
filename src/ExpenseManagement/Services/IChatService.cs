using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(string message, List<ChatMessageDto>? history = null);
}
