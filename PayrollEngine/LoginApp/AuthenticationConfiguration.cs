using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PayrollEngine.AuthorizationAndAuditApp;

namespace PayrollEngine.LoginApp;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddLoginAuthentication(
        this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "PayrollEngine.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode =
                        StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode =
                        StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static WebApplication UseLoginAuthentication(
     this WebApplication app)
    {
        app.UseAuthentication();
        app.UseMiddleware<HttpRequestAuditMiddleware>();
        app.UseHttpsRedirection();
        app.UseAuthorization();

        return app;
    }
}