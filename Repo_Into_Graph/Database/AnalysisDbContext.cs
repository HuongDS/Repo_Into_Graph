using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Repo_Into_Graph.Models;
using Repo_Into_Graph.Repo_Into_Graph.Models.Analysis;
using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlow;
using Repo_Into_Graph.Repo_Into_Graph.Models.BusinessFlows;
using Repo_Into_Graph.Repo_Into_Graph.Models.Feature;
using Repo_Into_Graph.Repo_Into_Graph.Models.FewShot;
using Repo_Into_Graph.Repo_Into_Graph.Models.Method;

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
    public DbSet<DataFlowEdge> DataFlowEdges { get; set; }

    public DbSet<FeatureRecord> FeatureRecords { get; set; }
    public DbSet<FeatureMethodMapping> FeatureMethodMappings { get; set; }

    public DbSet<BusinessFlow> BusinessFlows { get; set; }
    public DbSet<BusinessFlowStep> BusinessFlowSteps { get; set; }

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
            entity.HasMany(x => x.BusinessFlows)
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
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.AnalysisRunId);
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

        modelBuilder.Entity<FeatureRecord>(entity =>
        {
            entity.ToTable("feature_records");
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.AnalysisRun)
                .WithMany(p => p.Features)
                .HasForeignKey(d => d.AnalysisRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BusinessFlow>(entity =>
        {
            entity.ToTable("business_flows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired();
            entity.Property(x => x.EntryPoint).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasMany(x => x.Steps)
                .WithOne(x => x.BusinessFlow!)
                .HasForeignKey(x => x.BusinessFlowId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.AnalysisRunId);
        });

        modelBuilder.Entity<BusinessFlowStep>(entity =>
        {
            entity.ToTable("business_flow_steps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CallerClass).IsRequired();
            entity.Property(x => x.CallerMethod).IsRequired();
            entity.Property(x => x.CalleeClass).IsRequired();
            entity.Property(x => x.CalleeMethod).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.BusinessFlowId);
        });

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
            if (File.Exists(".env"))
            {
                DotNetEnv.Env.Load();
            }

            string dbUser = Environment.GetEnvironmentVariable("DB_USERNAME") ?? "postgres";
            string dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

            string baseConnectionString = configuration.GetConnectionString("PostgreSQL");
            string connectionString = $"{baseConnectionString.TrimEnd(';')};Username={dbUser};Password={dbPass};";


            optionsBuilder.UseNpgsql(connectionString);
        }
    }
}