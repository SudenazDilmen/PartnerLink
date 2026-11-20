using System.ComponentModel.DataAnnotations;
using PartnerLink.Core;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Enums;
using PartnerLink.Core.Models;

namespace PartnerLink.Web.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

/// <summary>The organization fields, shared by the registration form and the "edit profile" form.</summary>
public class OrganizationFormViewModel
{
    [Required, StringLength(200), Display(Name = "Organization name")]
    public string Name { get; set; } = string.Empty;

    [Required, Display(Name = "OID code (Organisation ID)")]
    [RegularExpression(@"^[Ee]\d{8}$", ErrorMessage = "The OID must be the letter E followed by 8 digits, e.g. E10123456.")]
    public string OidCode { get; set; } = string.Empty;

    [Required]
    public string Country { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Organization type")]
    public OrganizationType Type { get; set; }

    [Url, StringLength(300)]
    public string? Website { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public List<Sector> Sectors { get; set; } = [];
    public List<Priority> Priorities { get; set; } = [];

    [Display(Name = "We are open to new projects (shown to the matching engine)")]
    public bool OpenToNewProjects { get; set; } = true;

    public OrganizationInput ToInput() => new(
        Name, OidCode, Country, City, Type, Website, Description,
        EnumFlags.Combine(Sectors), EnumFlags.Combine(Priorities), OpenToNewProjects);

    public static OrganizationFormViewModel From(Organization organization) => new()
    {
        Name = organization.Name,
        OidCode = organization.OidCode,
        Country = organization.Country,
        City = organization.City,
        Type = organization.Type,
        Website = organization.Website,
        Description = organization.Description,
        Sectors = EnumFlags.Split(organization.Sectors).ToList(),
        Priorities = EnumFlags.Split(organization.Priorities).ToList(),
        OpenToNewProjects = organization.OpenToNewProjects
    };
}

public class RegisterViewModel : OrganizationFormViewModel
{
    [Required, StringLength(120), Display(Name = "Your name (contact person)")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Official organization e-mail")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProjectFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200), Display(Name = "Project title")]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(1000), Display(Name = "Short summary (English)")]
    public string Summary { get; set; } = string.Empty;

    [StringLength(4000), Display(Name = "Detailed description")]
    public string? Details { get; set; }

    [Display(Name = "Action type")]
    public ActionType ActionType { get; set; } = ActionType.KA210;

    [Display(Name = "Sector / target group")]
    public Sector Sector { get; set; } = Sector.Youth;

    public List<Priority> Priorities { get; set; } = [];

    /// <summary>Empty means "any Erasmus+ country".</summary>
    public List<string> TargetCountries { get; set; } = [];

    [DataType(DataType.Date), Display(Name = "Project application deadline")]
    public DateTime? ProjectDeadline { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Partner search ends on")]
    public DateTime? PartnerSearchEnd { get; set; }

    [StringLength(500), Display(Name = "Role of the partner")]
    public string? PartnerRole { get; set; }

    [StringLength(500), Display(Name = "Budget note (optional)")]
    public string? BudgetNote { get; set; }

    public ProjectInput ToInput() => new(
        Title, Summary, Details, ActionType, Sector,
        EnumFlags.Combine(Priorities), CountryList.Serialize(TargetCountries),
        ProjectDeadline, PartnerSearchEnd!.Value, PartnerRole, BudgetNote);

    public static ProjectFormViewModel From(Project project) => new()
    {
        Id = project.Id,
        Title = project.Title,
        Summary = project.Summary,
        Details = project.Details,
        ActionType = project.ActionType,
        Sector = project.Sector,
        Priorities = EnumFlags.Split(project.Priorities).ToList(),
        TargetCountries = CountryList.Parse(project.TargetCountries).ToList(),
        ProjectDeadline = project.ProjectDeadline,
        PartnerSearchEnd = project.PartnerSearchEnd,
        PartnerRole = project.PartnerRole,
        BudgetNote = project.BudgetNote
    };
}

public record HomeViewModel(Organization Organization, DashboardSummary Summary);

public record ProjectIndexViewModel(ProjectFilter Filter, PagedResult<ProjectCard> Result);

public record ProjectDetailsViewModel(
    Project Project,
    Organization? CurrentOrganization,
    bool IsOwner,
    PartnerApplication? ExistingApplication,
    IReadOnlyList<SuggestedPartner> Suggestions,
    OrganizationStats HostStats);

public class ApplyViewModel
{
    public int ProjectId { get; set; }

    [Required, StringLength(2000)]
    public string Message { get; set; } = string.Empty;
}

public record ChatRoomViewModel(
    PartnerApplication Application,
    Organization Me,
    Organization Other,
    IReadOnlyList<ChatMessage> Messages,
    bool CanSend,
    bool CanComplete,
    bool CanReview);

public class ReviewFormViewModel
{
    public int ApplicationId { get; set; }
    public string OtherOrganizationName { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;

    [Range(1, 5), Display(Name = "Communication (speed and quality)")]
    public int Communication { get; set; } = 5;

    [Range(1, 5), Display(Name = "Task management (tasks done on time)")]
    public int TaskManagement { get; set; } = 5;

    [Range(1, 5), Display(Name = "Reporting and bureaucracy (documents, invoices)")]
    public int Reporting { get; set; } = 5;

    [StringLength(2000), Display(Name = "Written review")]
    public string? Comment { get; set; }

    public ReviewInput ToInput() => new(Communication, TaskManagement, Reporting, Comment);
}

public record OrganizationDetailsViewModel(
    Organization Organization,
    OrganizationStats Stats,
    IReadOnlyList<Review> Reviews,
    bool IsMine);

public record AdminViewModel(
    IReadOnlyList<Organization> Pending,
    IReadOnlyList<Organization> Verified,
    IReadOnlyList<Organization> Rejected);
