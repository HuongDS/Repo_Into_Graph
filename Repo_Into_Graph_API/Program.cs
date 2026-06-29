using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repo_Into_Graph_API.Exceptions;
using Repo_Into_Graph_Application.Enums;
using Repo_Into_Graph_Application.Services.AI;
using Repo_Into_Graph_Application.Services.Analysis;
using Repo_Into_Graph_Application.Services.Caculation;
using Repo_Into_Graph_Application.Services.CodeQueryable;
using Repo_Into_Graph_Application.Services.DataFlowParser;
using Repo_Into_Graph_Application.Services.Features;
using Repo_Into_Graph_Application.Services.FewShot;
using Repo_Into_Graph_Application.Services.GitService;
using Repo_Into_Graph_Application.Services.Mapper;
using Repo_Into_Graph_Application.Services.QuestionGenerate;
using Repo_Into_Graph_DataAccess.Database;
using Repo_Into_Graph_DataAccess.Repository.Impl;
using Repo_Into_Graph_DataAccess.Repository.Interface;

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

// Register repositories & services under Dependency Injection
builder.Services.AddScoped<IAnalysisRunRepository, AnalysisRunRepository>();
builder.Services.AddScoped<ICallGraphEdgeRepository, CallGraphEdgeRepository>();
builder.Services.AddScoped<IMethodSourceRepository, MethodSourceRepository>();
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<IFeatureMethodMappingRepository, FeatureMethodMappingRepository>();
builder.Services.AddScoped<IFeatureRepository, FeatureRepository>();
builder.Services.AddScoped<IFeatureBusinessMappingRepository, FeatureBusinessMappingRepository>();
builder.Services.AddScoped<IFewShotExampleRepository, FewShotExampleRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<ICodeQueryable, CodeQueryable>();
builder.Services.AddScoped<GraphMapperService>();
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<IAnalysisRunService, AnalysisRunService>();
builder.Services.AddScoped<IFeatureService, FeatureService>();
builder.Services.AddScoped<IFewShotService, FewShotService>();
builder.Services.AddScoped<BusinessFlowParser>();
builder.Services.AddScoped<IQuestionGenerate, QuestionGenerate>();
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<DataFlowParseService>();
builder.Services.AddScoped<BusinessCallDataFlowGenerator>();
builder.Services.AddScoped<ICaculationService, CaculationService>();

// Add support for controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumDescriptionConverter<DifficultyLevel>());
    });

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

// Migrate Database on startup
using (var scope = app.Services.CreateScope())
{
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


