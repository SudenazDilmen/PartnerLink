using PartnerLink.Core;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Services;

namespace PartnerLink.Tests;

public class HelperTests
{
    [Theory]
    [InlineData("E10123456", true)]
    [InlineData("e10123456", true)]
    [InlineData(" E10123456 ", true)]
    [InlineData("E1012345", false)]
    [InlineData("X10123456", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Oid_format_is_validated(string? value, bool expected) =>
        Assert.Equal(expected, OidCode.IsValid(value));

    [Fact]
    public void Oid_is_normalized_to_upper_case() =>
        Assert.Equal("E10123456", OidCode.Normalize(" e10123456 "));

    [Fact]
    public void Country_list_round_trips()
    {
        var stored = CountryList.Serialize(["France", "Italy", "France"]);

        Assert.Equal("|France|Italy|", stored);
        Assert.Equal(["France", "Italy"], CountryList.Parse(stored));
    }

    [Fact]
    public void Empty_country_selection_means_any()
    {
        Assert.Equal(CountryList.Any, CountryList.Serialize([]));
        Assert.True(CountryList.Includes(CountryList.Any, "Spain"));
    }

    [Fact]
    public void Country_inclusion_matches_whole_names_only()
    {
        var stored = CountryList.Serialize(["North Macedonia"]);

        Assert.True(CountryList.Includes(stored, "North Macedonia"));
        Assert.False(CountryList.Includes(stored, "Macedonia"));
    }

    [Fact]
    public void Flags_split_and_combine_are_inverse()
    {
        var combined = EnumFlags.Combine([Sector.Youth, Sector.Sport]);

        Assert.Equal(Sector.Youth | Sector.Sport, combined);
        Assert.Equal([Sector.Youth, Sector.Sport], EnumFlags.Split(combined));
    }

    [Fact]
    public void Enum_labels_use_display_names_or_split_words()
    {
        Assert.Equal("Higher Education", Sector.HigherEducation.Label());
        Assert.Equal("Youth", Sector.Youth.Label());
        Assert.Equal("Inclusion and Diversity", Priority.Inclusion.Label());
    }
}

public class MatchScoringTests
{
    private static Project ProjectWith(Sector sector, Priority priorities, string countries = "Any") =>
        new() { Sector = sector, Priorities = priorities, TargetCountries = countries };

    private static Organization Org(Sector sectors, Priority priorities, string country = "Italy", decimal trust = 3.0m) =>
        new() { Sectors = sectors, Priorities = priorities, Country = country, TrustScore = trust };

    [Fact]
    public void Perfect_match_scores_high()
    {
        var project = ProjectWith(Sector.Youth, Priority.Environment, CountryList.Serialize(["Italy"]));
        var org = Org(Sector.Youth, Priority.Environment, "Italy", 5.0m);

        var (score, reasons, topic) = MatchScoring.Score(project, org);

        Assert.Equal(100, score);
        Assert.True(topic);
        Assert.NotEmpty(reasons);
    }

    [Fact]
    public void No_shared_sector_or_priority_is_not_a_topic_match()
    {
        var project = ProjectWith(Sector.Sport, Priority.Inclusion);
        var org = Org(Sector.Youth, Priority.Environment);

        var (_, _, topic) = MatchScoring.Score(project, org);

        Assert.False(topic);
    }

    [Fact]
    public void Priority_points_scale_with_the_share_of_matching_priorities()
    {
        var project = ProjectWith(Sector.None, Priority.Environment | Priority.Digital);
        var half = Org(Sector.Youth, Priority.Environment, trust: 0m);

        var (score, _, topic) = MatchScoring.Score(project, half);

        Assert.True(topic);
        // 25 * 1/2 = 12.5 priority points + 20 country points (Any) + 0 trust points
        Assert.Equal(33, score);
    }

    [Fact]
    public void Organizations_outside_the_target_countries_lose_the_country_points()
    {
        var project = ProjectWith(Sector.Youth, Priority.None, CountryList.Serialize(["France"]));
        var italian = Org(Sector.Youth, Priority.None, "Italy", 0m);
        var french = Org(Sector.Youth, Priority.None, "France", 0m);

        Assert.Equal(35, MatchScoring.Score(project, italian).Score);
        Assert.Equal(55, MatchScoring.Score(project, french).Score);
    }
}

public class TrustFormulaTests
{
    [Fact]
    public void New_organizations_start_neutral() =>
        Assert.Equal(3.00m, TrustService.Compute(null, 0));

    [Fact]
    public void Average_of_reviews_is_used_when_present() =>
        Assert.Equal(4.50m, TrustService.Compute(4.5, 0));

    [Fact]
    public void Each_ghosted_conversation_costs_half_a_point() =>
        Assert.Equal(3.50m, TrustService.Compute(4.5, 2));

    [Fact]
    public void Score_is_clamped_between_zero_and_five()
    {
        Assert.Equal(0m, TrustService.Compute(1.0, 5));
        Assert.Equal(5m, TrustService.Compute(5.0, 0));
    }
}
