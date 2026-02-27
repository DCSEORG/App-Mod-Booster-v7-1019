using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly IChatService _chatService;
    private readonly IConfiguration _configuration;

    public bool IsOpenAIConfigured { get; set; }

    public ChatModel(IChatService chatService, IConfiguration configuration)
    {
        _chatService = chatService;
        _configuration = configuration;
    }

    public void OnGet()
    {
        IsOpenAIConfigured = !string.IsNullOrEmpty(_configuration["OpenAI:Endpoint"]);
    }

    public async Task<IActionResult> OnPostAsync([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new JsonResult(new ChatResponse { Success = false, Message = "Message is required" });
        }

        var response = await _chatService.SendMessageAsync(request.Message, request.History);
        return new JsonResult(response);
    }
}
