using System.ComponentModel.DataAnnotations;

namespace PartnerLink.Core.Enums;

public enum OrganizationType
{
    [Display(Name = "NGO / Association")] NgoAssociation = 0,
    University = 1,
    School = 2,
    Company = 3,
    Municipality = 4,
    Other = 5
}

public enum VerificationStatus
{
    Pending = 0,
    Verified = 1,
    Rejected = 2
}

/// <summary>Erasmus+ sector / target group. Organizations can cover several (flags); a project has exactly one.</summary>
[Flags]
public enum Sector
{
    None = 0,
    Youth = 1,
    [Display(Name = "School Education")] SchoolEducation = 2,
    [Display(Name = "Higher Education")] HigherEducation = 4,
    [Display(Name = "VET")] Vet = 8,
    [Display(Name = "Adult Education")] AdultEducation = 16,
    Sport = 32
}

/// <summary>The four Erasmus+ horizontal priorities.</summary>
[Flags]
public enum Priority
{
    None = 0,
    [Display(Name = "Inclusion and Diversity")] Inclusion = 1,
    [Display(Name = "Digital Transformation")] Digital = 2,
    [Display(Name = "Environment and Climate")] Environment = 4,
    [Display(Name = "Democratic Participation")] Democracy = 8
}

public enum ActionType
{
    [Display(Name = "KA1 - Mobility")] KA1 = 0,
    [Display(Name = "KA210 - Small-scale partnership")] KA210 = 1,
    [Display(Name = "KA220 - Cooperation partnership")] KA220 = 2,
    Other = 3
}

public enum ProjectStatus
{
    Open = 0,
    Closed = 1
}

public enum ApplicationStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Completed = 3
}
