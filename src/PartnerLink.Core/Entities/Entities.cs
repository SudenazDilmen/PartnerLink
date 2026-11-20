using Microsoft.AspNetCore.Identity;
using PartnerLink.Core.Enums;

namespace PartnerLink.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Erasmus+ Organisation ID, format E + 8 digits.</summary>
    public string OidCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public OrganizationType Type { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }

    public Sector Sectors { get; set; }
    public Priority Priorities { get; set; }

    public VerificationStatus Verification { get; set; } = VerificationStatus.Pending;
    public bool OpenToNewProjects { get; set; } = true;

    /// <summary>0-5. Recomputed from reviews and ghosting events; 3.00 for organizations without history.</summary>
    public decimal TrustScore { get; set; } = 3.00m;

    public DateTime CreatedAtUtc { get; set; }

    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

/// <summary>A partner-search ad published by an organization.</summary>
public class Project
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Details { get; set; }

    public ActionType ActionType { get; set; }
    public Sector Sector { get; set; }
    public Priority Priorities { get; set; }

    /// <summary>"Any" or a pipe-delimited list such as "|France|Italy|" (see CountryList).</summary>
    public string TargetCountries { get; set; } = "Any";

    public DateTime? ProjectDeadline { get; set; }
    public DateTime PartnerSearchEnd { get; set; }

    public string? PartnerRole { get; set; }

    /// <summary>Optional free text; budget details are not mandatory.</summary>
    public string? BudgetNote { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Open;
    public DateTime CreatedAtUtc { get; set; }

    public int OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public ICollection<PartnerApplication> Applications { get; set; } = new List<PartnerApplication>();
}

/// <summary>An organization's request to join a project as a partner. Once accepted it doubles as the chat room.</summary>
public class PartnerApplication
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Set by the ghosting detector when this organization stopped answering in the chat.</summary>
    public int? GhostedOrganizationId { get; set; }
    public DateTime? GhostedAtUtc { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int ApplicantOrganizationId { get; set; }
    public Organization? Applicant { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

public class ChatMessage
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public int ApplicationId { get; set; }
    public PartnerApplication? Application { get; set; }

    public int SenderOrganizationId { get; set; }
    public Organization? Sender { get; set; }
}

public class Review
{
    public int Id { get; set; }

    public int Communication { get; set; }
    public int TaskManagement { get; set; }
    public int Reporting { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public int ApplicationId { get; set; }
    public PartnerApplication? Application { get; set; }

    public int ReviewerOrganizationId { get; set; }
    public Organization? Reviewer { get; set; }

    public int ReviewedOrganizationId { get; set; }
    public Organization? Reviewed { get; set; }

    public double Average => (Communication + TaskManagement + Reporting) / 3.0;
}

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
