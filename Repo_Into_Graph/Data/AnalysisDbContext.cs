using Microsoft.EntityFrameworkCore;

namespace Repo_Into_Graph.Data;

public class AnalysisDbContext : DbContext
{
    public AnalysisDbContext(DbContextOptions<AnalysisDbContext> options)
        : base(options)
    {
    }

    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<CallGraphEdgeRecord> CallGraphEdges => Set<CallGraphEdgeRecord>();
    public DbSet<MethodSourceRecord> MethodSources => Set<MethodSourceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisRun>(entity =>
        {
            entity.ToTable("analysis_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RepositoryPath).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasMany(x => x.CallGraphEdges)
                .WithOne(x => x.AnalysisRun!)
                .HasForeignKey(x => x.AnalysisRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.MethodSources)
                .WithOne(x => x.AnalysisRun!)
                .HasForeignKey(x => x.AnalysisRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CallGraphEdgeRecord>(entity =>
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
    }
}