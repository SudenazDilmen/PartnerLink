using Microsoft.EntityFrameworkCore;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;
using PartnerLink.Infrastructure.Persistence;
using PartnerLink.Infrastructure.Services;

namespace PartnerLink.Tests;

internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>An isolated in-memory database plus all services wired together, like the DI container does.</summary>
internal sealed class TestWorld
{
    public TestWorld()
    {
        Clock = new TestClock(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        Notifications = new NotificationService(Db, Clock);
        Trust = new TrustService(Db, Clock, Notifications);
        Projects = new ProjectService(Db, Clock);
        Applications = new ApplicationService(Db, Clock, Notifications);
        Chat = new ChatService(Db, Clock, Notifications);
        Reviews = new ReviewService(Db, Clock, Trust, Notifications);
        Match = new MatchService(Db, Notifications);
        Organizations = new OrganizationService(Db, Clock, Notifications);
    }

    public TestClock Clock { get; }
    public AppDbContext Db { get; }
    public NotificationService Notifications { get; }
    public TrustService Trust { get; }
    public ProjectService Projects { get; }
    public ApplicationService Applications { get; }
    public ChatService Chat { get; }
    public ReviewService Reviews { get; }
    public MatchService Match { get; }
    public OrganizationService Organizations { get; }

    public DateTime Today => Clock.Now.UtcDateTime.Date;

    private int _counter;

    public Organization AddOrganization(
        string name,
        string country = "Turkey",
        Sector sectors = Sector.Youth,
        Priority priorities = Priority.None,
        VerificationStatus status = VerificationStatus.Verified,
        bool open = true)
    {
        _counter++;
        var user = new ApplicationUser { Id = $"user-{_counter}", UserName = $"u{_counter}", Email = $"u{_counter}@test", FullName = name };
        Db.Users.Add(user);

        var organization = new Organization
        {
            Name = name,
            OidCode = $"E{10000000 + _counter}",
            Country = country,
            City = "City",
            Sectors = sectors,
            Priorities = priorities,
            Verification = status,
            OpenToNewProjects = open,
            OwnerId = user.Id,
            CreatedAtUtc = Clock.Now.UtcDateTime
        };
        Db.Organizations.Add(organization);
        Db.SaveChanges();
        return organization;
    }

    public Project AddProject(
        Organization owner,
        string title = "Project",
        Sector sector = Sector.Youth,
        Priority priorities = Priority.Environment,
        string targetCountries = "Any",
        int searchEndInDays = 20,
        ProjectStatus status = ProjectStatus.Open,
        ActionType action = ActionType.KA210)
    {
        var project = new Project
        {
            OrganizationId = owner.Id,
            Title = title,
            Summary = "Summary",
            ActionType = action,
            Sector = sector,
            Priorities = priorities,
            TargetCountries = targetCountries,
            PartnerSearchEnd = Today.AddDays(searchEndInDays),
            Status = status,
            CreatedAtUtc = Clock.Now.UtcDateTime
        };
        Db.Projects.Add(project);
        Db.SaveChanges();
        return project;
    }

    public PartnerApplication AddApplication(Project project, Organization applicant, ApplicationStatus status = ApplicationStatus.Pending)
    {
        var application = new PartnerApplication
        {
            ProjectId = project.Id,
            ApplicantOrganizationId = applicant.Id,
            Message = "Hello",
            Status = status,
            CreatedAtUtc = Clock.Now.UtcDateTime
        };
        Db.Applications.Add(application);
        Db.SaveChanges();
        return application;
    }

    public ChatMessage AddMessage(PartnerApplication application, Organization sender, DateTime sentAtUtc, string text = "Hi")
    {
        var message = new ChatMessage
        {
            ApplicationId = application.Id,
            SenderOrganizationId = sender.Id,
            Text = text,
            SentAtUtc = sentAtUtc
        };
        Db.ChatMessages.Add(message);
        Db.SaveChanges();
        return message;
    }

    public ProjectInput Input(
        Sector sector = Sector.Youth,
        Priority priorities = Priority.Digital,
        int searchEndInDays = 10,
        DateTime? deadline = null,
        string title = "New project") =>
        new(title, "Summary", null, ActionType.KA210, sector, priorities, "Any",
            deadline, Today.AddDays(searchEndInDays), null, null);
}
