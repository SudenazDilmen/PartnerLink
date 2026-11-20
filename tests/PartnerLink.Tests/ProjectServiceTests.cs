using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;

namespace PartnerLink.Tests;

public class ProjectServiceTests
{
    [Fact]
    public async Task Verified_organization_can_publish()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Verified Org");

        var result = await w.Projects.CreateAsync(org.Id, w.Input());

        Assert.True(result.Succeeded);
        Assert.Equal(ProjectStatus.Open, result.Value!.Status);
    }

    [Fact]
    public async Task Unverified_organization_cannot_publish()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Pending Org", status: VerificationStatus.Pending);

        var result = await w.Projects.CreateAsync(org.Id, w.Input());

        Assert.False(result.Succeeded);
        Assert.Empty(w.Db.Projects);
    }

    [Theory]
    [InlineData(Sector.Youth | Sector.Sport, Priority.Digital)]      // two sectors
    [InlineData(Sector.Youth, Priority.None)]                        // no priority
    [InlineData(Sector.Youth, Priority.Digital | Priority.Inclusion | Priority.Environment)] // three priorities
    public async Task Invalid_sector_or_priority_selection_is_rejected(Sector sector, Priority priorities)
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Org");

        var result = await w.Projects.CreateAsync(org.Id, w.Input(sector, priorities));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Dates_are_validated()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Org");

        var past = await w.Projects.CreateAsync(org.Id, w.Input(searchEndInDays: -1));
        var deadlineBeforeSearchEnd = await w.Projects.CreateAsync(org.Id, w.Input(searchEndInDays: 10, deadline: w.Today.AddDays(3)));

        Assert.False(past.Succeeded);
        Assert.False(deadlineBeforeSearchEnd.Succeeded);
    }

    [Fact]
    public async Task Only_the_owner_can_edit_or_close()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var other = w.AddOrganization("Other");
        var project = w.AddProject(owner);

        Assert.False((await w.Projects.UpdateAsync(project.Id, other.Id, w.Input())).Succeeded);
        Assert.False((await w.Projects.CloseAsync(project.Id, other.Id)).Succeeded);
        Assert.True((await w.Projects.CloseAsync(project.Id, owner.Id)).Succeeded);
        Assert.Equal(ProjectStatus.Closed, w.Db.Projects.Single().Status);
    }

    [Fact]
    public async Task Feed_hides_closed_and_expired_projects()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Org");
        w.AddProject(org, "Open");
        w.AddProject(org, "Closed", status: ProjectStatus.Closed);
        w.AddProject(org, "Expired", searchEndInDays: -3);

        var result = await w.Projects.SearchAsync(new ProjectFilter());

        Assert.Equal("Open", Assert.Single(result.Items).Title);
    }

    [Fact]
    public async Task Country_filter_returns_projects_targeting_that_country_or_any()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Org");
        w.AddProject(org, "ItalyOnly", targetCountries: "|Italy|");
        w.AddProject(org, "FranceOnly", targetCountries: "|France|");
        w.AddProject(org, "Anywhere", targetCountries: "Any");

        var result = await w.Projects.SearchAsync(new ProjectFilter { Country = "Italy" });

        Assert.Equal(["Anywhere", "ItalyOnly"], result.Items.Select(i => i.Title).OrderBy(t => t).ToList());
    }

    [Fact]
    public async Task Sector_priority_action_and_keyword_filters_combine()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Org");
        w.AddProject(org, "Green youth", Sector.Youth, Priority.Environment | Priority.Digital, action: ActionType.KA210);
        w.AddProject(org, "Green schools", Sector.SchoolEducation, Priority.Environment, action: ActionType.KA210);
        w.AddProject(org, "Digital youth", Sector.Youth, Priority.Digital, action: ActionType.KA220);

        var byPriority = await w.Projects.SearchAsync(new ProjectFilter { Priority = Priority.Environment });
        var byBoth = await w.Projects.SearchAsync(new ProjectFilter { Sector = Sector.Youth, Priority = Priority.Digital, ActionType = ActionType.KA210 });
        var byKeyword = await w.Projects.SearchAsync(new ProjectFilter { Query = "schools" });

        Assert.Equal(2, byPriority.TotalCount);
        Assert.Equal("Green youth", Assert.Single(byBoth.Items).Title);
        Assert.Equal("Green schools", Assert.Single(byKeyword.Items).Title);
    }

    [Fact]
    public async Task Feed_is_paged()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Org");
        for (var i = 0; i < 5; i++) w.AddProject(org, $"P{i}");

        var page2 = await w.Projects.SearchAsync(new ProjectFilter { Page = 2, PageSize = 2 });

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(2, page2.Items.Count);
        Assert.True(page2.HasNext);
        Assert.True(page2.HasPrevious);
    }
}
