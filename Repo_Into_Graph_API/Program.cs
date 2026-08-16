using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repo_Into_Graph_DataAccess.Repository.Impl;
using Repo_Into_Graph_DataAccess.Repository.Interface;
using Repo_Into_Graph_Application.Services.AI;
using Repo_Into_Graph_Application.Services.Analysis;
using Repo_Into_Graph_Application.Services.Features;
using Repo_Into_Graph_Application.Services.CodeQueryable;
using Repo_Into_Graph_Application.Services.DataFlowParser;
using Repo_Into_Graph_Application.Services.FewShot;
using Repo_Into_Graph_Application.Services.GitService;
using Repo_Into_Graph_Application.Services.Mapper;
using Repo_Into_Graph_Application.Services.QuestionGenerate;
using Repo_Into_Graph_API.Exceptions;
using Repo_Into_Graph_DataAccess.Database;
using Repo_Into_Graph_Application.Services.Caculation;
using Repo_Into_Graph_Application.Services.WorkflowAssessment;
using Repo_Into_Graph_API.Extensions;

if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

// ── Global Exception Handler ──────────────────────────────────────────────────
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Register HTTP Client Factory with HTTP/1.1 fallback policy to prevent HTTP/3 hang deadlocks
builder.Services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName)
    .ConfigureHttpClient(client =>
    {
        client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
        client.DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrLower;
    });

// Also register the named client "BaseModel" used by Mscc.GenerativeAI with the same HTTP/1.1 fallback policy
builder.Services.AddHttpClient("BaseModel", client =>
{
    client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
    client.DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrLower;
});

// Register DB Context
builder.Services.AddDbContext<AnalysisDbContext>();

// Register all project dependencies (Repositories & Services)
builder.Services.AddProjectDependencies();

// Add Redis Distributed Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
});

// Add support for controllers
builder.Services.AddControllers();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ── Global Exception Handler middleware (phải đứng đầu pipeline) ──────────────
app.UseExceptionHandler();

// Migrate Database — CHỈ chạy khi được yêu cầu tường minh (flag --migrate hoặc env RUN_MIGRATIONS=true),
// không tự động chạy trên mọi `dotnet run`. Muốn áp dụng migration mới: `dotnet run -- --migrate`
// hoặc set RUN_MIGRATIONS=true (tiện cho docker-compose/CI).
bool shouldRunMigrations =
    args.Contains("--migrate", StringComparer.OrdinalIgnoreCase) ||
    string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase);

if (shouldRunMigrations)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AnalysisDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("✅ PostgreSQL schema ready via Migrations.");
    }
    catch (Exception ex)
    {
        // Startup migration failure — chỉ log, không crash (giữ nguyên hành vi cũ)
        Console.WriteLine($"❌ Cannot prepare PostgreSQL schema: {ex.Message}");
    }
}
else
{
    Console.WriteLine("ℹ️  Bỏ qua migration (thêm --migrate hoặc set RUN_MIGRATIONS=true nếu cần áp dụng migration mới).");
}

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Repo Into Graph API V1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("AllowAll");

// Map controller routes
app.MapControllers();

app.Run();


