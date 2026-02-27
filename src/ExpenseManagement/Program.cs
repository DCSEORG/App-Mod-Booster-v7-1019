using ExpenseManagement.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Application Insights
// ============================================================
builder.Services.AddApplicationInsightsTelemetry();

// ============================================================
// Services
// ============================================================
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IChatService, ChatService>();

// ============================================================
// Razor Pages + Controllers
// ============================================================
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// ============================================================
// Swagger / OpenAPI
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Expense Management API",
        Version = "v1",
        Description = "REST API for the Expense Management application"
    });
});

// ============================================================
// HTTP Client
// ============================================================
builder.Services.AddHttpClient();

var app = builder.Build();

// ============================================================
// Middleware pipeline
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Redirect root to /Index
app.MapGet("/", () => Results.Redirect("/Index"));

app.Run();
