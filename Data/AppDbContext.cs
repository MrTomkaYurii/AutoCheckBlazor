using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<LabDef> Labs => Set<LabDef>();
    public DbSet<LabTask> LabTasks => Set<LabTask>();
    public DbSet<StudentRecord> Students => Set<StudentRecord>();
    public DbSet<TeacherRecord> Teachers => Set<TeacherRecord>();
    public DbSet<UserLink> UserLinks => Set<UserLink>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<TaskResult> TaskResults => Set<TaskResult>();
    public DbSet<DiffEntry> DiffEntries => Set<DiffEntry>();
    public DbSet<LabComment> Comments => Set<LabComment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<GroupRecord> Groups => Set<GroupRecord>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<LabDef>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.FullMarkdown).HasColumnType("TEXT");
            e.Property(x => x.Goal).HasColumnType("TEXT");
        });

        mb.Entity<LabTask>(e =>
        {
            e.Property(x => x.Brief).HasColumnType("TEXT");
            e.HasOne(x => x.LabDef).WithMany(x => x.Tasks)
                .HasForeignKey(x => x.LabDefId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<UserLink>(e =>
        {
            e.HasIndex(x => x.KeycloakSub).IsUnique();
            e.HasOne(x => x.Student).WithOne(x => x.UserLink)
                .HasForeignKey<UserLink>(x => x.StudentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Teacher).WithOne(x => x.UserLink)
                .HasForeignKey<UserLink>(x => x.TeacherId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<Submission>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.LabDefId }).IsUnique();
            e.HasOne(x => x.Student).WithMany(x => x.Submissions)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.LabDef).WithMany(x => x.Submissions)
                .HasForeignKey(x => x.LabDefId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<TaskResult>(e =>
        {
            e.Property(x => x.Feedback).HasColumnType("TEXT");
            e.HasOne(x => x.Submission).WithMany(x => x.TaskResults)
                .HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.LabTask).WithMany(x => x.Results)
                .HasForeignKey(x => x.LabTaskId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DiffEntry>(e =>
        {
            e.HasOne(x => x.TaskResult).WithMany(x => x.DiffLines)
                .HasForeignKey(x => x.TaskResultId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<LabComment>(e =>
        {
            e.Property(x => x.Text).HasColumnType("TEXT");
            e.HasOne(x => x.Submission).WithMany(x => x.Comments)
                .HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Notification>(e =>
        {
            e.HasOne(x => x.Student).WithMany(x => x.Notifications)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
