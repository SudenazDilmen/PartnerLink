using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartnerLink.Core.Entities;
using PartnerLink.Core.Services;
using PartnerLink.Infrastructure.Background;
using PartnerLink.Infrastructure.Persistence;
using PartnerLink.Infrastructure.Services;

namespace PartnerLink.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                // SQL Server in a container may still be starting; retry transient failures.
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITrustService, TrustService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddHostedService<TrustWorker>();

        return services;
    }
}
