using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SmkDoc.Api.Authentication;
using SmkDoc.Api.Constants;
using SmkDoc.Application.Common.Constants;

namespace SmkDoc.Api.Extensions;

internal static class ApiAuthenticationExtensions
{
    internal static void AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
        {
            throw new InvalidOperationException(
                "JWT Secret is required and must be at least 32 characters (256 bits). Set Jwt:Secret in appsettings.json or environment variables.");
        }

        int accessTokenMinutes = configuration.GetValue<int?>("Jwt:AccessTokenMinutes")
            ?? AuthConstants.DefaultAccessTokenMinutes;
        if (accessTokenMinutes < 1 || accessTokenMinutes > AuthConstants.MaxAccessTokenMinutes)
        {
            throw new InvalidOperationException(
                $"Jwt:AccessTokenMinutes must be between 1 and {AuthConstants.MaxAccessTokenMinutes} minutes (configured: {accessTokenMinutes}).");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "SmartScheme";
                options.DefaultAuthenticateScheme = "SmartScheme";
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme("SmartScheme", "Bearer or ApiKey", options =>
            {
                options.ForwardDefaultSelector = context =>
                    HttpHeaderConstants.TryExtractApiKey(context.Request.Headers, out _)
                        ? ApiKeyAuthenticationDefaults.AuthenticationScheme
                        : JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "SmkDoc",
                    ValidAudience = configuration["Jwt:Audience"] ?? "SmkDoc.Portal",
                    IssuerSigningKey = signingKey
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme, _ => { });

        services.AddAuthorization(ConfigureAuthorizationPolicies);
    }

    private static void ConfigureAuthorizationPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(AuthorizationPolicies.RequireSuperAdmin, policy =>
        {
            policy.RequireAssertion(ctx =>
                ctx.User.HasClaim("SystemRole", RoleConstants.SuperAdmin) ||
                ctx.User.IsInRole(RoleConstants.SuperAdmin));
        });

        options.AddPolicy(AuthorizationPolicies.RequireProjectAdmin, policy =>
        {
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(RoleConstants.Admin) ||
                HasReadWriteM2MScope(ctx));
        });

        options.AddPolicy(AuthorizationPolicies.CanManageTemplates, policy =>
        {
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(RoleConstants.Developer) ||
                ctx.User.IsInRole(RoleConstants.Admin) ||
                HasReadWriteM2MScope(ctx));
        });

        options.AddPolicy(AuthorizationPolicies.CanGenerateDocuments, policy =>
        {
            policy.RequireAssertion(ctx => ctx.User.Identity?.IsAuthenticated == true);
        });

        options.AddPolicy(AuthorizationPolicies.CanManageDataConnections, policy =>
        {
            policy.RequireAssertion(ctx => ctx.User.IsInRole(RoleConstants.Admin));
        });

        options.AddPolicy(AuthorizationPolicies.CanManageDatasets, policy =>
        {
            policy.RequireAssertion(ctx =>
                ctx.User.IsInRole(RoleConstants.Developer) ||
                ctx.User.IsInRole(RoleConstants.Admin) ||
                HasReadWriteM2MScope(ctx));
        });
    }

    private static bool HasReadWriteM2MScope(AuthorizationHandlerContext ctx) =>
        ctx.User.IsInRole("M2MClient") &&
        ctx.User.HasClaim(c => c.Type is "Scope" or "ApiKeyScope" && c.Value == "ReadWrite");
}
