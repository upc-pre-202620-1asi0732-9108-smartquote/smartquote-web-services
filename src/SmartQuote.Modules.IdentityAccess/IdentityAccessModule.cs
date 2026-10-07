using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.Modules.IdentityAccess.Application;
using SmartQuote.Modules.IdentityAccess.Application.Ports;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Repositories;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Security;

namespace SmartQuote.Modules.IdentityAccess;

public static class IdentityAccessModule
{
    public static IServiceCollection AddIdentityAccessModule(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddDbContext<IdentityAccessDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity_access")));

        services.AddSingleton(configuration.GetSection("Jwt").Get<JwtTokenOptions>() ?? new JwtTokenOptions());
        services.AddScoped<IIdentityAccessUnitOfWork, IdentityAccessUnitOfWork>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IRefreshSessionRepository, RefreshSessionRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddScoped<AuthenticationService>();

        return services;
    }

}
