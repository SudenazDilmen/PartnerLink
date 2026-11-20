using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartnerLink.Core;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Persistence;

namespace PartnerLink.Infrastructure.Seeding;

public static class DatabaseInitializer
{
    private const string DemoPassword = "Demo123!";

    /// <summary>Applies EF Core migrations (or creates the schema when none exist yet) and seeds base data.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        await SeedAsync(sp, ct);
    }

    private static async Task SeedAsync(IServiceProvider sp, CancellationToken ct)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var clock = sp.GetRequiredService<TimeProvider>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        await EnsureUserAsync(
            userManager, clock,
            email: config["Seed:AdminEmail"] ?? "admin@partnerlink.local",
            password: config["Seed:AdminPassword"] ?? "Admin123!",
            fullName: "Platform Admin",
            role: Roles.Admin);

        if (config.GetValue("Seed:DemoData", false))
            await SeedDemoDataAsync(sp, userManager, clock, ct);
    }

    private static async Task SeedDemoDataAsync(
        IServiceProvider sp, UserManager<ApplicationUser> userManager, TimeProvider clock, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        if (await db.Organizations.AnyAsync(ct)) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var today = now.Date;

        async Task<Organization> AddOrgAsync(
            string email, string contact, string name, string oid, string country, string city,
            Sector sectors, Priority priorities, string description, VerificationStatus status)
        {
            var user = await EnsureUserAsync(userManager, clock, email, DemoPassword, contact, Roles.Organization);
            var organization = new Organization
            {
                OwnerId = user.Id,
                Name = name,
                OidCode = oid,
                Country = country,
                City = city,
                Type = OrganizationType.NgoAssociation,
                Website = "https://example.org",
                Description = description,
                Sectors = sectors,
                Priorities = priorities,
                Verification = status,
                OpenToNewProjects = true,
                CreatedAtUtc = now.AddDays(-90)
            };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync(ct);
            return organization;
        }

        var istanbul = await AddOrgAsync("istanbul@demo.partnerlink.local", "Elif Yilmaz",
            "Youth Bridge Association", "E10123456", "Turkey", "Istanbul",
            Sector.Youth | Sector.Sport, Priority.Digital | Priority.Inclusion,
            "Youth NGO running exchanges and digital skills workshops for young people since 2016.",
            VerificationStatus.Verified);

        var turin = await AddOrgAsync("turin@demo.partnerlink.local", "Marco Rossi",
            "Verde Giovani APS", "E10234567", "Italy", "Turin",
            Sector.Youth, Priority.Environment | Priority.Democracy,
            "Italian association working on youth participation and urban green projects.",
            VerificationStatus.Verified);

        var valencia = await AddOrgAsync("valencia@demo.partnerlink.local", "Lucia Garcia",
            "Fundacion Horizonte", "E10345678", "Spain", "Valencia",
            Sector.Youth | Sector.AdultEducation, Priority.Digital | Priority.Inclusion | Priority.Environment,
            "Foundation with experience in coordinating Erasmus+ cooperation partnerships.",
            VerificationStatus.Verified);

        var lyon = await AddOrgAsync("lyon@demo.partnerlink.local", "Claire Dubois",
            "Les Jeunes Verts", "E10456789", "France", "Lyon",
            Sector.Youth | Sector.SchoolEducation, Priority.Environment,
            "Environmental education for schools and youth groups.",
            VerificationStatus.Verified);

        await AddOrgAsync("nordic@demo.partnerlink.local", "Anna Lindqvist",
            "Nordic Youth Lab", "E10567890", "Sweden", "Malmo",
            Sector.Youth, Priority.Digital,
            "New organization waiting for verification (use the admin account to approve it).",
            VerificationStatus.Pending);

        Project NewProject(Organization owner, string title, string summary, ActionType action, Sector sector,
            Priority priorities, string targets, int searchEndInDays, int? deadlineInDays, string role,
            string? budget, ProjectStatus status, int createdDaysAgo) => new()
        {
            OrganizationId = owner.Id,
            Title = title,
            Summary = summary,
            Details = summary + " We are looking for motivated partners with prior Erasmus+ experience or a strong wish to learn.",
            ActionType = action,
            Sector = sector,
            Priorities = priorities,
            TargetCountries = targets,
            PartnerSearchEnd = today.AddDays(searchEndInDays),
            ProjectDeadline = deadlineInDays is { } d ? today.AddDays(d) : null,
            PartnerRole = role,
            BudgetNote = budget,
            Status = status,
            CreatedAtUtc = now.AddDays(-createdDaysAgo)
        };

        var greenTech = NewProject(istanbul, "Green Tech for Youth",
            "A youth exchange combining climate action with digital tools: young people build simple apps and campaigns for sustainability.",
            ActionType.KA210, Sector.Youth, Priority.Environment | Priority.Digital,
            CountryList.Serialize(["France", "Italy"]), 25, 60,
            "Host a local workshop and send 5 participants.", "Small-scale partnership, grant up to EUR 30,000.",
            ProjectStatus.Open, 3);

        var sports = NewProject(istanbul, "Sports for Everyone",
            "Inclusive sports days and coach training for young people with fewer opportunities.",
            ActionType.KA1, Sector.Sport, Priority.Inclusion,
            CountryList.Any, 40, null,
            "Send coaches and participants.", null, ProjectStatus.Open, 8);

        var pastProject = NewProject(istanbul, "Youth Voices Online",
            "Online youth dialogues about democratic participation, with a digital toolkit as output.",
            ActionType.KA220, Sector.Youth, Priority.Democracy | Priority.Digital,
            CountryList.Any, -40, -10,
            "Co-author the toolkit.", null, ProjectStatus.Closed, 150);

        var storytelling = NewProject(turin, "Digital Storytelling for Inclusion",
            "Young people with fewer opportunities tell their stories through short digital media.",
            ActionType.KA220, Sector.Youth, Priority.Inclusion | Priority.Digital,
            CountryList.Any, 30, 75,
            "Run a local storytelling lab and join two transnational meetings.", "Cooperation partnership, indicative budget EUR 250,000.",
            ProjectStatus.Open, 5);

        var citizens = NewProject(valencia, "Active Citizens Lab",
            "Adult learners and youth workers co-design civic participation formats.",
            ActionType.KA220, Sector.AdultEducation, Priority.Democracy | Priority.Inclusion,
            CountryList.Serialize(["Turkey", "Italy", "France"]), 18, 50,
            "Pilot the learning formats with local groups.", null, ProjectStatus.Open, 2);

        var zeroWaste = NewProject(lyon, "Schools Going Zero Waste",
            "Primary and secondary schools reduce waste with student-led projects and teacher training.",
            ActionType.KA210, Sector.SchoolEducation, Priority.Environment,
            CountryList.Any, 12, 45,
            "Share good practice from your school network.", "Small-scale partnership.", ProjectStatus.Open, 1);

        db.Projects.AddRange(greenTech, sports, pastProject, storytelling, citizens, zeroWaste);
        await db.SaveChangesAsync(ct);

        // Turin was accepted on "Green Tech for Youth": an active chat room with two messages.
        var accepted = new PartnerApplication
        {
            ProjectId = greenTech.Id,
            ApplicantOrganizationId = turin.Id,
            Message = "We run green youth projects in Turin and would love to host a workshop on urban gardening.",
            Status = ApplicationStatus.Accepted,
            CreatedAtUtc = now.AddDays(-2),
            RespondedAtUtc = now.AddDays(-2).AddHours(3)
        };

        // Lyon is still waiting for an answer.
        var pending = new PartnerApplication
        {
            ProjectId = greenTech.Id,
            ApplicantOrganizationId = lyon.Id,
            Message = "Our school network is keen to test your digital sustainability tools.",
            Status = ApplicationStatus.Pending,
            CreatedAtUtc = now.AddHours(-6)
        };

        // Two finished partnerships on the old project, each with reviews.
        var doneValencia = new PartnerApplication
        {
            ProjectId = pastProject.Id,
            ApplicantOrganizationId = valencia.Id,
            Message = "We coordinated three similar toolkits.",
            Status = ApplicationStatus.Completed,
            CreatedAtUtc = now.AddDays(-140),
            RespondedAtUtc = now.AddDays(-139),
            CompletedAtUtc = now.AddDays(-20)
        };
        var doneTurin = new PartnerApplication
        {
            ProjectId = pastProject.Id,
            ApplicantOrganizationId = turin.Id,
            Message = "Youth participation is our core topic.",
            Status = ApplicationStatus.Completed,
            CreatedAtUtc = now.AddDays(-138),
            RespondedAtUtc = now.AddDays(-137),
            CompletedAtUtc = now.AddDays(-20)
        };

        db.Applications.AddRange(accepted, pending, doneValencia, doneTurin);
        await db.SaveChangesAsync(ct);

        db.ChatMessages.AddRange(
            new ChatMessage
            {
                ApplicationId = accepted.Id,
                SenderOrganizationId = turin.Id,
                Text = "Hi! Thanks for accepting us. Could you share the draft activity plan?",
                SentAtUtc = now.AddDays(-2).AddHours(4),
                ReadAtUtc = now.AddDays(-2).AddHours(5)
            },
            new ChatMessage
            {
                ApplicationId = accepted.Id,
                SenderOrganizationId = istanbul.Id,
                Text = "Welcome aboard! I will upload it this week. Which dates work for the first online meeting?",
                SentAtUtc = now.AddDays(-2).AddHours(6)
            });

        db.Reviews.AddRange(
            NewReview(doneValencia, istanbul, valencia, 5, 5, 4, "Fast answers and very organised reporting.", now.AddDays(-15)),
            NewReview(doneValencia, valencia, istanbul, 5, 4, 5, "Great coordination, clear tasks.", now.AddDays(-14)),
            NewReview(doneTurin, istanbul, turin, 4, 5, 4, "Reliable partner, delivered on time.", now.AddDays(-15)));
        await db.SaveChangesAsync(ct);

        await sp.GetRequiredService<ITrustService>().RecalculateAllAsync(ct);
    }

    private static Review NewReview(PartnerApplication application, Organization reviewer, Organization reviewed,
        int communication, int tasks, int reporting, string comment, DateTime createdAtUtc) => new()
    {
        ApplicationId = application.Id,
        ReviewerOrganizationId = reviewer.Id,
        ReviewedOrganizationId = reviewed.Id,
        Communication = communication,
        TaskManagement = tasks,
        Reporting = reporting,
        Comment = comment,
        CreatedAtUtc = createdAtUtc
    };

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, TimeProvider clock,
        string email, string password, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                $"Could not seed user {email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
