using Microsoft.EntityFrameworkCore;
using PartnerLink.Core;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Services;

public sealed class ProjectService(AppDbContext db, TimeProvider clock) : IProjectService
{
    public async Task<PagedResult<ProjectCard>> SearchAsync(ProjectFilter filter, CancellationToken ct = default)
    {
        var today = clock.GetUtcNow().UtcDateTime.Date;

        // The feed only shows open projects whose partner search is still running.
        var query = db.Projects
            .AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Open && p.PartnerSearchEnd >= today);

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim();
            query = query.Where(p => p.Title.Contains(term) || p.Summary.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            // "Partner country": projects that look for partners from this country (or from anywhere).
            var marker = "|" + filter.Country.Trim() + "|";
            query = query.Where(p => p.TargetCountries == CountryList.Any || p.TargetCountries.Contains(marker));
        }

        if (filter.ActionType is { } action)
            query = query.Where(p => p.ActionType == action);

        if (filter.Sector is { } sector)
            query = query.Where(p => p.Sector == sector);

        if (filter.Priority is { } priority)
            query = query.Where(p => p.Priorities.HasFlag(priority)); // bitwise AND in SQL

        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        var page = Math.Max(1, filter.Page);
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProjectCard(
                p.Id,
                p.Title,
                p.Summary,
                p.ActionType,
                p.Sector,
                p.Priorities,
                p.PartnerSearchEnd,
                p.TargetCountries,
                p.OrganizationId,
                p.Organization!.Name,
                p.Organization!.Country,
                p.Organization!.Verification == VerificationStatus.Verified,
                p.Organization!.TrustScore))
            .ToListAsync(ct);

        return new PagedResult<ProjectCard>(items, page, pageSize, total);
    }

    public Task<Project?> GetAsync(int id, CancellationToken ct = default) =>
        db.Projects.AsNoTracking().Include(p => p.Organization).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> GetByOrganizationAsync(int organizationId, CancellationToken ct = default) =>
        await db.Projects
            .AsNoTracking()
            .Include(p => p.Applications)
            .Where(p => p.OrganizationId == organizationId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<OperationResult<Project>> CreateAsync(int organizationId, ProjectInput input, CancellationToken ct = default)
    {
        var organization = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (organization is null)
            return OperationResult<Project>.Fail("Organization not found.");
        if (organization.Verification != VerificationStatus.Verified)
            return OperationResult<Project>.Fail("Your organization must be verified before you can publish a project.");

        var invalid = Validate(input);
        if (invalid is not null)
            return OperationResult<Project>.Fail(invalid);

        var project = new Project
        {
            OrganizationId = organizationId,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            Status = ProjectStatus.Open
        };
        Apply(project, input);

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return OperationResult<Project>.Ok(project);
    }

    public async Task<OperationResult> UpdateAsync(int id, int organizationId, ProjectInput input, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);
        if (project is null) return OperationResult.Fail("Project not found.");

        var invalid = Validate(input);
        if (invalid is not null) return OperationResult.Fail(invalid);

        Apply(project, input);
        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> CloseAsync(int id, int organizationId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);
        if (project is null) return OperationResult.Fail("Project not found.");

        project.Status = ProjectStatus.Closed;
        await db.SaveChangesAsync(ct);
        return OperationResult.Ok();
    }

    private string? Validate(ProjectInput input)
    {
        if (EnumFlags.Split(input.Sector).Count != 1)
            return "Choose exactly one sector.";

        var priorities = EnumFlags.Split(input.Priorities).Count;
        if (priorities is < 1 or > 2)
            return "Choose one or two priorities.";

        var today = clock.GetUtcNow().UtcDateTime.Date;
        if (input.PartnerSearchEnd.Date < today)
            return "The partner search end date cannot be in the past.";

        if (input.ProjectDeadline is { } deadline && deadline.Date < input.PartnerSearchEnd.Date)
            return "The project deadline should not be before the partner search end date.";

        return null;
    }

    private static void Apply(Project project, ProjectInput input)
    {
        project.Title = input.Title.Trim();
        project.Summary = input.Summary.Trim();
        project.Details = string.IsNullOrWhiteSpace(input.Details) ? null : input.Details.Trim();
        project.ActionType = input.ActionType;
        project.Sector = input.Sector;
        project.Priorities = input.Priorities;
        project.TargetCountries = input.TargetCountries;
        project.ProjectDeadline = input.ProjectDeadline?.Date;
        project.PartnerSearchEnd = input.PartnerSearchEnd.Date;
        project.PartnerRole = string.IsNullOrWhiteSpace(input.PartnerRole) ? null : input.PartnerRole.Trim();
        project.BudgetNote = string.IsNullOrWhiteSpace(input.BudgetNote) ? null : input.BudgetNote.Trim();
    }
}
