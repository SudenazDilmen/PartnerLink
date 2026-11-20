using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;

namespace PartnerLink.Tests;

public class ApplicationServiceTests
{
    [Fact]
    public async Task Verified_organization_can_apply_and_the_owner_is_notified()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var project = w.AddProject(owner);

        var result = await w.Applications.ApplyAsync(project.Id, applicant.Id, "  We would love to join.  ");

        Assert.True(result.Succeeded);
        Assert.Equal("We would love to join.", result.Value!.Message);
        var notification = Assert.Single(w.Db.Notifications);
        Assert.Equal(owner.OwnerId, notification.UserId);
    }

    [Fact]
    public async Task Application_rules_are_enforced()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var unverified = w.AddOrganization("Unverified", status: VerificationStatus.Pending);
        var open = w.AddProject(owner, "Open");
        var closed = w.AddProject(owner, "Closed", status: ProjectStatus.Closed);

        Assert.False((await w.Applications.ApplyAsync(open.Id, owner.Id, "Hi")).Succeeded);          // own project
        Assert.False((await w.Applications.ApplyAsync(open.Id, unverified.Id, "Hi")).Succeeded);     // not verified
        Assert.False((await w.Applications.ApplyAsync(closed.Id, applicant.Id, "Hi")).Succeeded);     // closed
        Assert.False((await w.Applications.ApplyAsync(open.Id, applicant.Id, "   ")).Succeeded);      // empty message

        Assert.True((await w.Applications.ApplyAsync(open.Id, applicant.Id, "Hi")).Succeeded);
        Assert.False((await w.Applications.ApplyAsync(open.Id, applicant.Id, "Again")).Succeeded);    // duplicate
    }

    [Fact]
    public async Task Only_the_owner_can_answer_and_only_once()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var stranger = w.AddOrganization("Stranger");
        var application = w.AddApplication(w.AddProject(owner), applicant);

        Assert.False((await w.Applications.RespondAsync(application.Id, stranger.Id, true)).Succeeded);
        Assert.False((await w.Applications.RespondAsync(application.Id, applicant.Id, true)).Succeeded);

        Assert.True((await w.Applications.RespondAsync(application.Id, owner.Id, true)).Succeeded);
        Assert.Equal(ApplicationStatus.Accepted, w.Db.Applications.Single().Status);
        Assert.NotNull(w.Db.Applications.Single().RespondedAtUtc);

        Assert.False((await w.Applications.RespondAsync(application.Id, owner.Id, false)).Succeeded);
    }

    [Fact]
    public async Task Rejected_applications_never_open_a_chat()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var application = w.AddApplication(w.AddProject(owner), applicant);

        await w.Applications.RespondAsync(application.Id, owner.Id, false);

        Assert.False(await w.Chat.CanAccessAsync(application.Id, owner.Id));
        Assert.False(await w.Chat.CanAccessAsync(application.Id, applicant.Id));
    }

    [Fact]
    public async Task Only_active_partnerships_can_be_completed_by_a_participant()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var stranger = w.AddOrganization("Stranger");
        var pending = w.AddApplication(w.AddProject(owner), applicant);
        var accepted = w.AddApplication(w.AddProject(owner, "Second"), applicant, ApplicationStatus.Accepted);

        Assert.False((await w.Applications.CompleteAsync(pending.Id, owner.Id)).Succeeded);
        Assert.False((await w.Applications.CompleteAsync(accepted.Id, stranger.Id)).Succeeded);
        Assert.True((await w.Applications.CompleteAsync(accepted.Id, applicant.Id)).Succeeded);
        Assert.Equal(ApplicationStatus.Completed, w.Db.Applications.Single(a => a.Id == accepted.Id).Status);
    }

    [Fact]
    public async Task Incoming_and_outgoing_lists_are_scoped_to_the_organization()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var other = w.AddOrganization("Other");
        w.AddApplication(w.AddProject(owner), applicant);

        Assert.Single(await w.Applications.GetIncomingAsync(owner.Id));
        Assert.Empty(await w.Applications.GetIncomingAsync(other.Id));
        Assert.Single(await w.Applications.GetOutgoingAsync(applicant.Id));
        Assert.Empty(await w.Applications.GetOutgoingAsync(owner.Id));
    }
}

public class ChatServiceTests
{
    private static (TestWorld World, Organization Owner, Organization Applicant, PartnerApplication Application) ActiveChat()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var application = w.AddApplication(w.AddProject(owner), applicant, ApplicationStatus.Accepted);
        return (w, owner, applicant, application);
    }

    [Fact]
    public async Task Participants_can_chat_and_strangers_cannot()
    {
        var (w, owner, applicant, application) = ActiveChat();
        var stranger = w.AddOrganization("Stranger");

        Assert.True((await w.Chat.SendAsync(application.Id, applicant.Id, "Hello")).Succeeded);
        Assert.True((await w.Chat.SendAsync(application.Id, owner.Id, "Welcome")).Succeeded);
        Assert.False((await w.Chat.SendAsync(application.Id, stranger.Id, "Let me in")).Succeeded);
        Assert.False(await w.Chat.CanAccessAsync(application.Id, stranger.Id));
        Assert.Empty(await w.Chat.GetMessagesAsync(application.Id, stranger.Id));
    }

    [Fact]
    public async Task Messages_are_validated_and_trimmed()
    {
        var (w, _, applicant, application) = ActiveChat();

        Assert.False((await w.Chat.SendAsync(application.Id, applicant.Id, "   ")).Succeeded);
        Assert.False((await w.Chat.SendAsync(application.Id, applicant.Id, new string('x', 2001))).Succeeded);

        var ok = await w.Chat.SendAsync(application.Id, applicant.Id, "  hi  ");
        Assert.Equal("hi", ok.Value!.Text);
    }

    [Fact]
    public async Task Opening_the_room_marks_the_other_sides_messages_as_read()
    {
        var (w, owner, applicant, application) = ActiveChat();
        await w.Chat.SendAsync(application.Id, applicant.Id, "Hello");

        var asApplicant = await w.Chat.GetMessagesAsync(application.Id, applicant.Id);
        Assert.Null(asApplicant.Single().ReadAtUtc);

        var asOwner = await w.Chat.GetMessagesAsync(application.Id, owner.Id);
        Assert.NotNull(asOwner.Single().ReadAtUtc);
    }

    [Fact]
    public async Task Several_messages_create_only_one_unread_notification()
    {
        var (w, owner, applicant, application) = ActiveChat();

        await w.Chat.SendAsync(application.Id, applicant.Id, "One");
        await w.Chat.SendAsync(application.Id, applicant.Id, "Two");
        await w.Chat.SendAsync(application.Id, applicant.Id, "Three");

        Assert.Equal(1, await w.Notifications.UnreadCountAsync(owner.OwnerId));
    }

    [Fact]
    public async Task Completed_partnerships_are_read_only()
    {
        var (w, owner, applicant, application) = ActiveChat();
        await w.Applications.CompleteAsync(application.Id, owner.Id);

        Assert.True(await w.Chat.CanAccessAsync(application.Id, applicant.Id));
        Assert.False((await w.Chat.SendAsync(application.Id, applicant.Id, "Too late")).Succeeded);
    }

    [Fact]
    public async Task Notifications_can_be_marked_as_read()
    {
        var (w, owner, applicant, application) = ActiveChat();
        await w.Chat.SendAsync(application.Id, applicant.Id, "Hello");

        await w.Notifications.MarkAllReadAsync(owner.OwnerId);

        Assert.Equal(0, await w.Notifications.UnreadCountAsync(owner.OwnerId));
    }
}

public class ReviewAndTrustTests
{
    private static (TestWorld World, Organization Owner, Organization Applicant, PartnerApplication Application) Finished()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var application = w.AddApplication(w.AddProject(owner), applicant, ApplicationStatus.Completed);
        return (w, owner, applicant, application);
    }

    [Fact]
    public async Task Reviews_are_only_possible_for_completed_partnerships()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var active = w.AddApplication(w.AddProject(owner), applicant, ApplicationStatus.Accepted);

        Assert.False(await w.Reviews.CanReviewAsync(active.Id, owner.Id));
        Assert.False((await w.Reviews.CreateAsync(active.Id, owner.Id, new ReviewInput(5, 5, 5, null))).Succeeded);
    }

    [Fact]
    public async Task A_review_updates_the_trust_score_of_the_reviewed_organization()
    {
        var (w, owner, applicant, application) = Finished();

        var result = await w.Reviews.CreateAsync(application.Id, applicant.Id, new ReviewInput(5, 5, 5, " Great! "));

        Assert.True(result.Succeeded);
        Assert.Equal(5.00m, w.Db.Organizations.Single(o => o.Id == owner.Id).TrustScore);
        Assert.Equal("Great!", w.Db.Reviews.Single().Comment);
        Assert.Equal(3.00m, w.Db.Organizations.Single(o => o.Id == applicant.Id).TrustScore);
    }

    [Fact]
    public async Task Both_sides_review_once_and_scores_must_be_between_one_and_five()
    {
        var (w, owner, applicant, application) = Finished();

        Assert.False((await w.Reviews.CreateAsync(application.Id, owner.Id, new ReviewInput(0, 5, 5, null))).Succeeded);
        Assert.False((await w.Reviews.CreateAsync(application.Id, owner.Id, new ReviewInput(5, 6, 5, null))).Succeeded);

        Assert.True((await w.Reviews.CreateAsync(application.Id, owner.Id, new ReviewInput(4, 4, 4, null))).Succeeded);
        Assert.False(await w.Reviews.CanReviewAsync(application.Id, owner.Id));
        Assert.True(await w.Reviews.CanReviewAsync(application.Id, applicant.Id));
        Assert.Equal(4.00m, w.Db.Organizations.Single(o => o.Id == applicant.Id).TrustScore);
    }

    [Fact]
    public async Task Organization_stats_summarize_reviews_and_partnerships()
    {
        var (w, owner, applicant, application) = Finished();
        await w.Reviews.CreateAsync(application.Id, applicant.Id, new ReviewInput(5, 4, 3, null));

        var stats = await w.Organizations.GetStatsAsync(owner.Id);

        Assert.Equal(1, stats.ReviewCount);
        Assert.Equal(4.0, stats.AverageRating, 3);
        Assert.Equal(1, stats.CompletedPartnerships);
        Assert.Equal(0, stats.GhostedConversations);
    }

    [Fact]
    public async Task A_conversation_left_unanswered_flags_the_silent_partner_and_lowers_their_score()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var application = w.AddApplication(w.AddProject(owner), applicant, ApplicationStatus.Accepted);
        w.AddMessage(application, owner, w.Clock.Now.UtcDateTime.AddDays(-6)); // owner wrote 6 days ago, no answer

        var flagged = await w.Trust.DetectGhostingAsync(TimeSpan.FromDays(5));

        Assert.Equal(1, flagged);
        var stored = w.Db.Applications.Single();
        Assert.Equal(applicant.Id, stored.GhostedOrganizationId);
        Assert.Equal(2.50m, w.Db.Organizations.Single(o => o.Id == applicant.Id).TrustScore);
        Assert.Equal(3.00m, w.Db.Organizations.Single(o => o.Id == owner.Id).TrustScore);
    }

    [Fact]
    public async Task Fresh_conversations_are_not_flagged_and_a_flag_is_only_set_once()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var applicant = w.AddOrganization("Applicant");
        var application = w.AddApplication(w.AddProject(owner), applicant, ApplicationStatus.Accepted);
        w.AddMessage(application, owner, w.Clock.Now.UtcDateTime.AddDays(-2));

        Assert.Equal(0, await w.Trust.DetectGhostingAsync(TimeSpan.FromDays(5)));

        w.Clock.Now = w.Clock.Now.AddDays(10);
        Assert.Equal(1, await w.Trust.DetectGhostingAsync(TimeSpan.FromDays(5)));
        Assert.Equal(0, await w.Trust.DetectGhostingAsync(TimeSpan.FromDays(5)));
        Assert.Equal(2.50m, w.Db.Organizations.Single(o => o.Id == applicant.Id).TrustScore);
    }
}

public class OrganizationAndMatchTests
{
    [Fact]
    public async Task Admin_verification_changes_the_status_and_notifies_the_owner()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Org", status: VerificationStatus.Pending);

        await w.Organizations.SetVerificationAsync(org.Id, VerificationStatus.Verified);

        Assert.Equal(VerificationStatus.Verified, w.Db.Organizations.Single().Verification);
        Assert.Equal(1, await w.Notifications.UnreadCountAsync(org.OwnerId));
        Assert.Single(await w.Organizations.GetByStatusAsync(VerificationStatus.Verified));
        Assert.Empty(await w.Organizations.GetByStatusAsync(VerificationStatus.Pending));
    }

    [Fact]
    public async Task Changing_the_oid_sends_the_organization_back_to_verification()
    {
        var w = new TestWorld();
        var org = w.AddOrganization("Org");
        var input = new OrganizationInput("Org", "e99999999", "Turkey", "Istanbul", OrganizationType.NgoAssociation,
            null, null, Sector.Youth, Priority.None, true);

        var result = await w.Organizations.UpdateAsync(org.Id, input);

        Assert.True(result.Succeeded);
        var stored = w.Db.Organizations.Single();
        Assert.Equal("E99999999", stored.OidCode);
        Assert.Equal(VerificationStatus.Pending, stored.Verification);
    }

    [Fact]
    public async Task An_oid_can_only_be_used_by_one_organization()
    {
        var w = new TestWorld();
        var first = w.AddOrganization("First");
        var second = w.AddOrganization("Second");

        Assert.True(await w.Organizations.OidExistsAsync(first.OidCode));
        Assert.False(await w.Organizations.OidExistsAsync(first.OidCode, first.Id));

        var input = new OrganizationInput("Second", first.OidCode, "Turkey", "City", OrganizationType.School,
            null, null, Sector.Youth, Priority.None, true);
        Assert.False((await w.Organizations.UpdateAsync(second.Id, input)).Succeeded);
    }

    [Fact]
    public async Task Suggestions_only_include_verified_open_organizations_with_a_topic_match()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var project = w.AddProject(owner, sector: Sector.Youth, priorities: Priority.Environment);

        var strong = w.AddOrganization("Strong match", "Italy", Sector.Youth, Priority.Environment);
        var weak = w.AddOrganization("Weak match", "Spain", Sector.Youth, Priority.None);
        w.AddOrganization("Unverified", "Italy", Sector.Youth, Priority.Environment, VerificationStatus.Pending);
        w.AddOrganization("Closed to new projects", "Italy", Sector.Youth, Priority.Environment, open: false);
        w.AddOrganization("Different topic", "Italy", Sector.Sport, Priority.Inclusion);

        var suggestions = await w.Match.SuggestAsync(project.Id, owner.Id, take: 5);

        Assert.Equal([strong.Id, weak.Id], suggestions.Select(s => s.OrganizationId).ToList());
        Assert.True(suggestions[0].Score > suggestions[1].Score);
    }

    [Fact]
    public async Task Organizations_that_already_applied_are_not_suggested_and_only_the_owner_sees_suggestions()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var project = w.AddProject(owner, sector: Sector.Youth, priorities: Priority.Environment);
        var applied = w.AddOrganization("Applied", "Italy", Sector.Youth, Priority.Environment);
        var stranger = w.AddOrganization("Stranger", "Italy", Sector.Youth, Priority.Environment);
        w.AddApplication(project, applied);

        var forOwner = await w.Match.SuggestAsync(project.Id, owner.Id);
        var forStranger = await w.Match.SuggestAsync(project.Id, stranger.Id);

        Assert.Equal(stranger.Id, Assert.Single(forOwner).OrganizationId);
        Assert.Empty(forStranger);
    }

    [Fact]
    public async Task Invitations_notify_the_invited_organization_once()
    {
        var w = new TestWorld();
        var owner = w.AddOrganization("Owner");
        var invited = w.AddOrganization("Invited");
        var project = w.AddProject(owner);

        Assert.True((await w.Match.InviteAsync(project.Id, owner.Id, invited.Id)).Succeeded);
        Assert.True((await w.Match.InviteAsync(project.Id, owner.Id, invited.Id)).Succeeded);
        Assert.False((await w.Match.InviteAsync(project.Id, invited.Id, owner.Id)).Succeeded);

        Assert.Equal(1, await w.Notifications.UnreadCountAsync(invited.OwnerId));
    }
}
