using PartnerLink.Core.Entities;

namespace PartnerLink.Core.Services;

/// <summary>
/// Rule-based partner matching. Pure function, so it is trivial to unit test.
/// Max 100 points: sector 35, priorities 25, country 20, trust 20.
/// </summary>
public static class MatchScoring
{
    public static (int Score, List<string> Reasons, bool TopicMatch) Score(Project project, Organization candidate)
    {
        var reasons = new List<string>();
        double score = 0;
        var topicMatch = false;

        if (project.Sector != Enums.Sector.None && candidate.Sectors.HasFlag(project.Sector))
        {
            score += 35;
            topicMatch = true;
            reasons.Add($"Works in {project.Sector.Label()}");
        }

        var wanted = EnumFlags.Split(project.Priorities);
        if (wanted.Count > 0)
        {
            var shared = wanted.Where(p => candidate.Priorities.HasFlag(p)).ToList();
            if (shared.Count > 0)
            {
                score += 25.0 * shared.Count / wanted.Count;
                topicMatch = true;
                reasons.Add("Shares priority: " + string.Join(", ", shared.Select(p => p.Label())));
            }
        }

        if (CountryList.Includes(project.TargetCountries, candidate.Country))
        {
            score += 20;
            if (project.TargetCountries != CountryList.Any)
                reasons.Add($"Based in {candidate.Country}, a target country");
        }

        score += 20.0 * (double)candidate.TrustScore / 5.0;
        if (candidate.TrustScore >= 4.0m)
            reasons.Add($"Trust score {candidate.TrustScore:0.0}/5");

        return ((int)Math.Round(score), reasons, topicMatch);
    }
}
