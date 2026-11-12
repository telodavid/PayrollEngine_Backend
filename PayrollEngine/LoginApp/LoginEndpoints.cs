using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PayrollEngine.AppCatalog;
using PayrollEngine.AuthorizationAndAuditApp;
using PayrollEngine.RoleCheckAndLogApp;

namespace PayrollEngine.LoginApp;

public static class LoginEndpoints
{
    public static WebApplication MapLoginEndpoints(
        this WebApplication app)
    {
        app.MapPost("/api/login", LoginAsync)
            .WithName("Login");

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserLoginRepository users,
        UserAccessRepository userAccessRepository,
        AppModuleCatalog appModuleCatalog,
        IAuditLogWriter auditLog,
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("LoginEndpoints");
        LoginUser? user = null;

        try
        {
            user = await users.FindByUserNameAsync(
                request.UserName,
                cancellationToken);

            if (user is null ||
                !user.IsActive ||
                user.Password != request.Password)
            {
                await WriteLoginAuditSafelyAsync(
                    auditLog,
                    logger,
                    user?.UserId,
                    request.UserName,
                    false,
                    "Sikertelen bejelentkezés.",
                    cancellationToken);

                return Results.Unauthorized();
            }

            var access = await userAccessRepository.GetUserAccessAsync(
                user.UserId,
                user.UserName,
                cancellationToken);

            var availableModules =
                appModuleCatalog.GetAvailableModules(access);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.UserName)
            };

            claims.AddRange(
                access.Roles.Select(role =>
                    new Claim(ClaimTypes.Role, role.Code)));

            claims.AddRange(
                access.Apps.Select(app =>
                    new Claim("app", app.Code)));

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            await WriteLoginAuditSafelyAsync(
                auditLog,
                logger,
                user.UserId,
                user.UserName,
                true,
                "Sikeres bejelentkezés.",
                cancellationToken);

            return Results.Ok(new
            {
                user.UserId,
                user.UserName,
                roles = access.Roles,
                apps = availableModules,
                message = "Sikeres bejelentkezés."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Hiba történt a bejelentkezés feldolgozásakor. " +
                "UserName: {UserName}",
                request.UserName);

            await WriteLoginAuditSafelyAsync(
                auditLog,
                logger,
                user?.UserId,
                request.UserName,
                false,
                "A bejelentkezés feldolgozása hibával megszakadt.",
                cancellationToken);

            return Results.Problem(
                title: "A bejelentkezés feldolgozása sikertelen.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task WriteLoginAuditSafelyAsync(
        IAuditLogWriter auditLog,
        ILogger logger,
        int? userId,
        string? userName,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLogEntry(
            DateTimeOffset.UtcNow,
            userId,
            userName,
            "LOGIN",
            "Authentication",
            userId?.ToString(),
            null,
            null,
            success,
            message);

        try
        {
            await auditLog.WriteAsync(
                entry,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "A bejelentkezési auditbejegyzés írása sikertelen. " +
                "UserName: {UserName}",
                userName);
        }
    }
}