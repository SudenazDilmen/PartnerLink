using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PartnerLink.Core;

/// <summary>Helpers for [Flags] enums that are edited as checkbox groups.</summary>
public static class EnumFlags
{
    public static IReadOnlyList<T> Split<T>(T value) where T : struct, Enum =>
        Enum.GetValues<T>()
            .Where(v => Convert.ToInt64(v) != 0 && value.HasFlag(v))
            .ToList();

    public static T Combine<T>(IEnumerable<T> values) where T : struct, Enum
    {
        long result = 0;
        foreach (var value in values)
            result |= Convert.ToInt64(value);
        return (T)Enum.ToObject(typeof(T), result);
    }

    public static IReadOnlyList<T> SingleValues<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Where(v => Convert.ToInt64(v) != 0).ToList();
}

public static class EnumDisplay
{
    /// <summary>Human readable label: the [Display] name when present, otherwise the member name split into words.</summary>
    public static string Label(this Enum value)
    {
        var name = value.ToString();
        var member = value.GetType().GetField(name);
        var display = member?.GetCustomAttribute<DisplayAttribute>();
        return display?.Name ?? Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
    }
}

public static class OidCode
{
    private static readonly Regex Pattern = new(@"^E\d{8}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsValid(string? value) => !string.IsNullOrWhiteSpace(value) && Pattern.IsMatch(value.Trim());

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

public static class Countries
{
    /// <summary>Erasmus+ programme countries: EU member states plus the associated countries.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        "Austria", "Belgium", "Bulgaria", "Croatia", "Cyprus", "Czechia", "Denmark", "Estonia",
        "Finland", "France", "Germany", "Greece", "Hungary", "Iceland", "Ireland", "Italy",
        "Latvia", "Liechtenstein", "Lithuania", "Luxembourg", "Malta", "Netherlands",
        "North Macedonia", "Norway", "Poland", "Portugal", "Romania", "Serbia", "Slovakia",
        "Slovenia", "Spain", "Sweden", "Turkey"
    ];
}

/// <summary>Stores the "target countries" of a project in one column: "Any" or "|France|Italy|".</summary>
public static class CountryList
{
    public const string Any = "Any";

    public static string Serialize(IEnumerable<string>? countries)
    {
        var list = (countries ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();
        if (list.Count == 0 || list.Contains(Any)) return Any;
        return "|" + string.Join("|", list) + "|";
    }

    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrEmpty(value) || value == Any) return Array.Empty<string>();
        return value.Split('|', StringSplitOptions.RemoveEmptyEntries);
    }

    public static bool Includes(string? value, string country) =>
        string.IsNullOrEmpty(value) || value == Any || value.Contains("|" + country + "|");

    public static string Describe(string? value)
    {
        var list = Parse(value);
        return list.Count == 0 ? "Any Erasmus+ country" : string.Join(", ", list);
    }
}
