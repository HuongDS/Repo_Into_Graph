using Microsoft.EntityFrameworkCore;
using Repo_Into_Graph_DataAccess.Models;
using Repo_Into_Graph_DataAccess.Models.Analysis;
using Repo_Into_Graph_DataAccess.Models.Business;
using Repo_Into_Graph_DataAccess.Models.Feature;
using Repo_Into_Graph_DataAccess.Models.FewShot;
using Repo_Into_Graph_DataAccess.Models.Method;
using Microsoft.Extensions.Configuration;
using BusinessModel = Repo_Into_Graph_DataAccess.Models.Business.Bussiness;
using FeatureModel = Repo_Into_Graph_DataAccess.Models.Feature.Feature;

namespace Repo_Into_Graph_DataAccess.Database;

public class AnalysisDbContext : DbContext
{
    public AnalysisDbContext()
    {
    }
    public AnalysisDbContext(DbContextOptions<AnalysisDbContext> options)
        : base(options)
    {
    }

    public DbSet<AnalysisRun> AnalysisRuns { get; set; }
    public DbSet<CallGraphEdge> CallGraphEdges { get; set; }
    public DbSet<MethodSourceRecord> MethodSources { get; set; }

    // Business (nhom chuc nang tu template)
    public DbSet<BusinessModel> Businesses { get; set; }
    public DbSet<FeatureMethodMapping> FeatureMethodMappings { get; set; }

    // Feature (luong xu ly duoc phan tich tu call graph)
    public DbSet<FeatureModel> Features { get; set; }
    public DbSet<FeatureStep> FeatureSteps { get; set; }

    // Bảng nhiều-nhiều Feature ↔ Business
    public DbSet<FeatureBusinessMapping> FeatureBusinessMappings { get; set; }

    public DbSet<FewShotExample> FewShotExamples { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisRun>(entity =>
        {
            entity.ToTable("analysis_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RepositoryPath).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

            // Repository metadata columns
            entity.Property(x => x.RepoName).HasMaxLength(255);
            entity.Property(x => x.RepoOwner).HasMaxLength(255);
            entity.Property(x => x.RepoDescription).HasMaxLength(1000);
            entity.Property(x => x.RepoUrl).HasMaxLength(500);
            entity.Property(x => x.RepoLanguage).HasMaxLength(100);
            entity.Property(x => x.RepoStars);
            entity.Property(x => x.IsPublic);
            entity.Property(x => x.RepoUpdatedAt);

            entity.HasMany(x => x.CallGraphEdges)
                .WithOne(x => x.AnalysisRun!)
                .HasForeignKey(x => x.AnalysisRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.MethodSources)
                .WithOne(x => x.AnalysisRun!)
                .HasForeignKey(x => x.AnalysisRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Features)
                .WithOne(x => x.AnalysisRun!)
                .HasForeignKey(x => x.AnalysisRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Businesses)
                .WithOne(x => x.AnalysisRun!)
                .HasForeignKey(x => x.AnalysisRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CallGraphEdge>(entity =>
        {
            entity.ToTable("call_graph_edges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CallerClass).IsRequired();
            entity.Property(x => x.CallerMethod).IsRequired();
            entity.Property(x => x.CalleeClass).IsRequired();
            entity.Property(x => x.CalleeMethod).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.AnalysisRunId);
        });

        modelBuilder.Entity<MethodSourceRecord>(entity =>
        {
            entity.ToTable("method_sources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClassName).IsRequired();
            entity.Property(x => x.MethodName).IsRequired();
            entity.Property(x => x.SourceCode).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasDefaultValue(Repo_Into_Graph_DataAccess.Consts.NodeType.Activity);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.AnalysisRunId);
        });

        // Business (nhom chuc nang)
        modelBuilder.Entity<BusinessModel>(entity =>
        {
            entity.ToTable("businesses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BusinessName).IsRequired();
            entity.Property(e => e.CreatedAt);
            entity.HasIndex(e => e.AnalysisRunId);
        });

        modelBuilder.Entity<FeatureMethodMapping>(entity =>
        {
            entity.ToTable("feature_method_mappings");
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.Feature)
                .WithMany(p => p.FeatureMethodMappings)
                .HasForeignKey(d => d.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.MethodSource)
                .WithMany(p => p.FeatureMethodMappings)
                .HasForeignKey(d => d.MethodSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Feature (luong phan tich)
        modelBuilder.Entity<FeatureModel>(entity =>
        {
            entity.ToTable("features");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.EntryPoint).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasMany(x => x.Steps)
                .WithOne(x => x.Feature!)
                .HasForeignKey(x => x.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.AnalysisRunId);
        });

        modelBuilder.Entity<FeatureStep>(entity =>
        {
            entity.ToTable("feature_steps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CallerClass).IsRequired();
            entity.Property(x => x.CallerMethod).IsRequired();
            entity.Property(x => x.CalleeClass).IsRequired();
            entity.Property(x => x.CalleeMethod).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.FeatureId);
        });

        // ─── FeatureBusinessMapping (nhiều-nhiều) ────────────────────────────────
        modelBuilder.Entity<FeatureBusinessMapping>(entity =>
        {
            entity.ToTable("feature_business_mappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Feature)
                .WithMany(p => p.FeatureBusinessMappings)
                .HasForeignKey(d => d.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Business)
                .WithMany(p => p.FeatureBusinessMappings)
                .HasForeignKey(d => d.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── FewShot ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<FewShotExample>(entity =>
        {
            entity.ToTable("few_shot_examples");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Question).IsRequired();
            entity.Property(x => x.SuggestedAnswer).IsRequired();
            entity.Property(x => x.Difficulty).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Tag).HasMaxLength(100);
            entity.Property(x => x.Description);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Difficulty);
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Dò .env ở CẢ thư mục hiện hành LẪN thư mục chứa .dll (bin), vì
            // `dotnet run` và chạy trực tiếp .exe có thư mục hiện hành khác nhau.
            foreach (var dir in ProbeDirectories())
            {
                var envFile = Path.Combine(dir, ".env");
                if (File.Exists(envFile))
                {
                    DotNetEnv.Env.Load(envFile);
                    break;
                }
            }

            string dbUser = Environment.GetEnvironmentVariable("DB_USERNAME") ?? "postgres";
            string dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";

            // Nạp cấu hình giống cách Host của ASP.NET nạp: appsettings.json +
            // appsettings.<Environment>.json + biến môi trường. Tất cả đều optional và
            // dò ở nhiều thư mục, nên chạy từ đâu cũng không văng FileNotFoundException.
            string environmentName =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? "Development";

            var configBuilder = new ConfigurationBuilder();
            foreach (var dir in ProbeDirectories())
            {
                configBuilder.AddJsonFile(Path.Combine(dir, "appsettings.json"), optional: true, reloadOnChange: false);
                configBuilder.AddJsonFile(Path.Combine(dir, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: false);
            }
            var configuration = configBuilder.Build();

            // Ưu tiên biến môi trường (đọc trực tiếp để không phải thêm package
            // Microsoft.Extensions.Configuration.EnvironmentVariables vào project).
            string baseConnectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL")
                ?? configuration.GetConnectionString("PostgreSQL");
            if (string.IsNullOrWhiteSpace(baseConnectionString))
            {
                throw new InvalidOperationException(
                    "Không tìm thấy ConnectionStrings:PostgreSQL. Đã dò appsettings.json và " +
                    $"appsettings.{environmentName}.json tại: " + string.Join(" | ", ProbeDirectories()) +
                    ". Hãy đặt file cấu hình vào một trong các thư mục đó, hoặc set biến môi trường " +
                    "ConnectionStrings__PostgreSQL.");
            }

            string connectionString = $"{baseConnectionString.TrimEnd(';')};Username={dbUser};Password={dbPass};";

            optionsBuilder.UseNpgsql(connectionString);
        }
    }

    /// <summary>
    /// Thư mục sẽ dò file cấu hình: thư mục hiện hành (khi chạy `dotnet run`) và
    /// thư mục chứa assembly (bin/Debug/net8.0 — nơi appsettings được copy ra khi build).
    /// </summary>
    private static IEnumerable<string> ProbeDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var full = Path.GetFullPath(dir);
            if (seen.Add(full)) yield return full;
        }
    }
}
