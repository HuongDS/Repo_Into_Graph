using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Impl;
using Repo_Into_Graph.Repo_Into_Graph.Repository.Interface;
using Repo_Into_Graph.Repo_Into_Graph.Services.AI;
using Repo_Into_Graph.Repo_Into_Graph.Services.Analysis;
using Repo_Into_Graph.Repo_Into_Graph.Services.BusinessFlows;
using Repo_Into_Graph.Repo_Into_Graph.Services.CodeQueryable;
using Repo_Into_Graph.Repo_Into_Graph.Services.DataFlowParser;
using Repo_Into_Graph.Repo_Into_Graph.Services.GitService;
using Repo_Into_Graph.Repo_Into_Graph.Services.Mapper;
using Repo_Into_Graph.Repo_Into_Graph.Services.QuestionGenerate;

if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IFeatureRepository, FeatureRepository>();
builder.Services.AddScoped<IFeatureMethodMappingRepository, FeatureMethodMappingRepository>();
builder.Services.AddScoped<IFewShotExampleRepository, FewShotExampleRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<ICodeQueryable, CodeQueryable>();
builder.Services.AddScoped<GraphMapperService>();
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<IAnalysisRunService, AnalysisRunService>();
builder.Services.AddScoped<IBusinessFlowService, BusinessFlowService>();
builder.Services.AddScoped<Repo_Into_Graph.Repo_Into_Graph.Services.DataFlowParser.BusinessFlowParser>();
builder.Services.AddScoped<IQuestionGenerate, QuestionGenerate>();
builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<DataFlowParseService>();
builder.Services.AddScoped<BusinessCallDataFlowGenerator>();
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