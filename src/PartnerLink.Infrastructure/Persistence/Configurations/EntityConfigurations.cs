using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PartnerLink.Core.Entities;

namespace PartnerLink.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(120).IsRequired();
        builder.Property(u => u.CreatedAtUtc).HasColumnType("datetime2");
    }
}

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.OidCode).HasMaxLength(9).IsRequired();
        builder.Property(o => o.Country).HasMaxLength(60).IsRequired();
        builder.Property(o => o.City).HasMaxLength(100).IsRequired();
        builder.Property(o => o.Website).HasMaxLength(300);
        builder.Property(o => o.Description).HasMaxLength(2000);
        builder.Property(o => o.TrustScore).HasPrecision(3, 2);
        builder.Property(o => o.CreatedAtUtc).HasColumnType("datetime2");

        // Enums that describe a single state are stored as readable text.
        builder.Property(o => o.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(o => o.Verification).HasConversion<string>().HasMaxLength(20);
        // Sectors / Priorities are [Flags] enums: stored as int, filtered with bitwise operators in SQL.

        builder.HasOne(o => o.Owner)
            .WithMany()
            .HasForeignKey(o => o.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.OidCode).IsUnique();
        builder.HasIndex(o => o.OwnerId).IsUnique();
        builder.HasIndex(o => o.Verification);
    }
}

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(p => p.Details).HasMaxLength(4000);
        builder.Property(p => p.TargetCountries).HasMaxLength(600).IsRequired();
        builder.Property(p => p.PartnerRole).HasMaxLength(500);
        builder.Property(p => p.BudgetNote).HasMaxLength(500);

        builder.Property(p => p.ActionType).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(p => p.ProjectDeadline).HasColumnType("date");
        builder.Property(p => p.PartnerSearchEnd).HasColumnType("date");
        builder.Property(p => p.CreatedAtUtc).HasColumnType("datetime2");

        builder.HasOne(p => p.Organization)
            .WithMany(o => o.Projects)
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // The feed only shows open projects whose partner search has not ended.
        builder.HasIndex(p => new { p.Status, p.PartnerSearchEnd });
        builder.HasIndex(p => p.OrganizationId);
    }
}

public class PartnerApplicationConfiguration : IEntityTypeConfiguration<PartnerApplication>
{
    public void Configure(EntityTypeBuilder<PartnerApplication> builder)
    {
        builder.ToTable("Applications");

        builder.Property(a => a.Message).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(a => a.RespondedAtUtc).HasColumnType("datetime2");
        builder.Property(a => a.CompletedAtUtc).HasColumnType("datetime2");
        builder.Property(a => a.GhostedAtUtc).HasColumnType("datetime2");

        builder.HasOne(a => a.Project)
            .WithMany(p => p.Applications)
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Applicant)
            .WithMany()
            .HasForeignKey(a => a.ApplicantOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // One application per organization and project.
        builder.HasIndex(a => new { a.ProjectId, a.ApplicantOrganizationId }).IsUnique();
        builder.HasIndex(a => new { a.ApplicantOrganizationId, a.Status });
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");

        builder.Property(m => m.Text).HasMaxLength(2000).IsRequired();
        builder.Property(m => m.SentAtUtc).HasColumnType("datetime2");
        builder.Property(m => m.ReadAtUtc).HasColumnType("datetime2");

        builder.HasOne(m => m.Application)
            .WithMany(a => a.Messages)
            .HasForeignKey(m => m.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.ApplicationId, m.SentAtUtc });
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", t =>
        {
            t.HasCheckConstraint("CK_Reviews_Communication", "[Communication] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Reviews_TaskManagement", "[TaskManagement] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Reviews_Reporting", "[Reporting] BETWEEN 1 AND 5");
        });

        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetime2");
        builder.Ignore(r => r.Average);

        builder.HasOne(r => r.Application)
            .WithMany(a => a.Reviews)
            .HasForeignKey(r => r.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Reviewed)
            .WithMany()
            .HasForeignKey(r => r.ReviewedOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.ApplicationId, r.ReviewerOrganizationId }).IsUnique();
        builder.HasIndex(r => r.ReviewedOrganizationId);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.Property(n => n.UserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.Text).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Url).HasMaxLength(300).IsRequired();
        builder.Property(n => n.CreatedAtUtc).HasColumnType("datetime2");

        builder.HasIndex(n => new { n.UserId, n.IsRead });
    }
}
