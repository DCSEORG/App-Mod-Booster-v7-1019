using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace ExpenseManagement.Services;

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;
    private readonly ICategoryService _categoryService;
    private readonly IUserService _userService;

    public ChatService(
        IConfiguration configuration,
        ILogger<ChatService> logger,
        IExpenseService expenseService,
        ICategoryService categoryService,
        IUserService userService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        _categoryService = categoryService;
        _userService = userService;
    }

    public async Task<ChatResponse> SendMessageAsync(string message, List<ChatMessageDto>? history = null)
    {
        var openAIEndpoint = _configuration["OpenAI:Endpoint"];
        var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrEmpty(openAIEndpoint))
        {
            return new ChatResponse
            {
                Success = false,
                Message = "Azure OpenAI is not configured. Deploy with the -DeployGenAI switch to enable AI chat:\n\n" +
                          "```\n.\\deploy-infra\\deploy.ps1 -ResourceGroup \"rg-yourapp\" -Location \"uksouth\" -DeployGenAI\n```",
                Error = "OpenAI endpoint not configured"
            };
        }

        try
        {
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            Azure.Core.TokenCredential credential;

            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential for OpenAI");
                credential = new DefaultAzureCredential();
            }

            var openAIClient = new AzureOpenAIClient(new Uri(openAIEndpoint), credential);
            var chatClient = openAIClient.GetChatClient(deploymentName);

            // Build tools (function calling)
            var tools = BuildTools();

            // Build messages
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(GetSystemPrompt())
            };

            // Add conversation history
            if (history != null)
            {
                foreach (var h in history)
                {
                    messages.Add(h.Role == "user"
                        ? ChatMessage.CreateUserMessage(h.Content)
                        : ChatMessage.CreateAssistantMessage(h.Content));
                }
            }

            messages.Add(ChatMessage.CreateUserMessage(message));

            // Function calling loop
            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            var continueLoop = true;
            string? finalResponse = null;

            while (continueLoop)
            {
                var completion = await chatClient.CompleteChatAsync(messages, options);
                var choice = completion.Value;

                if (choice.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // Execute tool calls
                    messages.Add(ChatMessage.CreateAssistantMessage(choice));

                    var toolResultMessages = new List<ToolChatMessage>();

                    foreach (var toolCall in choice.ToolCalls)
                    {
                        _logger.LogInformation("Executing tool: {ToolName}", toolCall.FunctionName);
                        var result = await ExecuteToolAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                        toolResultMessages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                    }

                    messages.AddRange(toolResultMessages);
                }
                else
                {
                    finalResponse = choice.Content.FirstOrDefault()?.Text ?? "I couldn't generate a response.";
                    continueLoop = false;
                }
            }

            return new ChatResponse
            {
                Success = true,
                Message = finalResponse ?? "No response generated."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure OpenAI");
            return new ChatResponse
            {
                Success = false,
                Message = $"Sorry, I encountered an error: {ex.Message}",
                Error = ex.Message
            };
        }
    }

    private List<ChatTool> BuildTools()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_expenses",
                "Retrieves expenses from the database. Can filter by user ID or status.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "userId": { "type": "integer", "description": "Filter by user ID (optional)" },
                        "statusId": { "type": "integer", "description": "Filter by status ID: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected (optional)" }
                    }
                }
                """)),

            ChatTool.CreateFunctionTool(
                "get_expense_by_id",
                "Gets details of a specific expense by its ID.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expenseId": { "type": "integer", "description": "The expense ID" }
                    },
                    "required": ["expenseId"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "create_expense",
                "Creates a new expense record. Amounts should be in pence (e.g. £25.40 = 2540).",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "userId": { "type": "integer", "description": "User ID for the expense" },
                        "categoryId": { "type": "integer", "description": "Category ID: 1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other" },
                        "amountMinor": { "type": "integer", "description": "Amount in pence (e.g. £25.40 = 2540)" },
                        "expenseDate": { "type": "string", "description": "Expense date in YYYY-MM-DD format" },
                        "description": { "type": "string", "description": "Description of the expense" }
                    },
                    "required": ["userId", "categoryId", "amountMinor", "expenseDate"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "get_expense_stats",
                "Gets summary statistics about expenses including counts by status and total amounts.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "userId": { "type": "integer", "description": "Filter stats by user ID (optional)" }
                    }
                }
                """)),

            ChatTool.CreateFunctionTool(
                "get_users",
                "Gets all active users in the system.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {}
                }
                """)),

            ChatTool.CreateFunctionTool(
                "get_categories",
                "Gets all available expense categories.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {}
                }
                """)),

            ChatTool.CreateFunctionTool(
                "submit_expense",
                "Submits an expense for manager approval.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expenseId": { "type": "integer", "description": "The expense ID to submit" }
                    },
                    "required": ["expenseId"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "approve_expense",
                "Approves a submitted expense.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expenseId": { "type": "integer", "description": "The expense ID to approve" },
                        "reviewedBy": { "type": "integer", "description": "User ID of the manager approving the expense" }
                    },
                    "required": ["expenseId", "reviewedBy"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "reject_expense",
                "Rejects a submitted expense.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expenseId": { "type": "integer", "description": "The expense ID to reject" },
                        "reviewedBy": { "type": "integer", "description": "User ID of the manager rejecting the expense" }
                    },
                    "required": ["expenseId", "reviewedBy"]
                }
                """))
        };
    }

    private async Task<string> ExecuteToolAsync(string toolName, string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var args = doc.RootElement;

            switch (toolName)
            {
                case "get_expenses":
                {
                    int? userId = args.TryGetProperty("userId", out var uidEl) ? uidEl.GetInt32() : null;
                    int? statusId = args.TryGetProperty("statusId", out var sidEl) ? sidEl.GetInt32() : null;
                    var expenses = await _expenseService.GetAllExpensesAsync(userId, statusId);
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId, e.UserName, e.CategoryName, e.StatusName,
                        e.AmountFormatted, e.ExpenseDate, e.Description
                    }));
                }

                case "get_expense_by_id":
                {
                    var expenseId = args.GetProperty("expenseId").GetInt32();
                    var expense = await _expenseService.GetExpenseByIdAsync(expenseId);
                    return expense == null ? "Expense not found" : JsonSerializer.Serialize(expense);
                }

                case "create_expense":
                {
                    var request = new Models.CreateExpenseRequest
                    {
                        UserId = args.GetProperty("userId").GetInt32(),
                        CategoryId = args.GetProperty("categoryId").GetInt32(),
                        AmountMinor = args.GetProperty("amountMinor").GetInt32(),
                        ExpenseDate = DateTime.Parse(args.GetProperty("expenseDate").GetString()!),
                        Description = args.TryGetProperty("description", out var descEl) ? descEl.GetString() : null
                    };
                    var newId = await _expenseService.CreateExpenseAsync(request);
                    return JsonSerializer.Serialize(new { success = true, expenseId = newId, message = $"Expense created with ID {newId}" });
                }

                case "get_expense_stats":
                {
                    int? userId = args.TryGetProperty("userId", out var uidEl) ? uidEl.GetInt32() : null;
                    var stats = await _expenseService.GetExpenseStatsAsync(userId);
                    return JsonSerializer.Serialize(stats);
                }

                case "get_users":
                {
                    var users = await _userService.GetAllUsersAsync();
                    return JsonSerializer.Serialize(users.Select(u => new { u.UserId, u.UserName, u.Email, u.RoleName }));
                }

                case "get_categories":
                {
                    var categories = await _categoryService.GetAllCategoriesAsync();
                    return JsonSerializer.Serialize(categories);
                }

                case "submit_expense":
                {
                    var expenseId = args.GetProperty("expenseId").GetInt32();
                    var result = await _expenseService.SubmitExpenseAsync(expenseId);
                    return JsonSerializer.Serialize(new { success = result, message = result ? $"Expense {expenseId} submitted for approval" : "Failed to submit expense" });
                }

                case "approve_expense":
                {
                    var expenseId = args.GetProperty("expenseId").GetInt32();
                    var reviewedBy = args.GetProperty("reviewedBy").GetInt32();
                    var result = await _expenseService.ApproveExpenseAsync(expenseId, reviewedBy);
                    return JsonSerializer.Serialize(new { success = result, message = result ? $"Expense {expenseId} approved" : "Failed to approve expense" });
                }

                case "reject_expense":
                {
                    var expenseId = args.GetProperty("expenseId").GetInt32();
                    var reviewedBy = args.GetProperty("reviewedBy").GetInt32();
                    var result = await _expenseService.RejectExpenseAsync(expenseId, reviewedBy);
                    return JsonSerializer.Serialize(new { success = result, message = result ? $"Expense {expenseId} rejected" : "Failed to reject expense" });
                }

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static string GetSystemPrompt() => """
        You are an intelligent expense management assistant. You help users manage their business expenses.

        You have access to the following functions:
        - get_expenses: Retrieve expenses (optionally filtered by user or status)
        - get_expense_by_id: Get details of a specific expense
        - create_expense: Create a new expense record
        - get_expense_stats: Get summary statistics
        - get_users: List all users
        - get_categories: List expense categories (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)
        - submit_expense: Submit an expense for manager approval
        - approve_expense: Approve a submitted expense (manager action)
        - reject_expense: Reject a submitted expense (manager action)

        Status IDs: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected

        When displaying lists of expenses or data, format them clearly with relevant details.
        For monetary amounts, remember they are stored in pence (divide by 100 to get pounds, or multiply by 100 to convert pounds to pence).
        Always confirm with users before making changes (create, submit, approve, reject).
        Be helpful, concise, and professional.
        """;
}
