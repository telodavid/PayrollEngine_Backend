using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PayrollEngine;

public static class DatabaseStartupCheck
{
    public static async Task CheckDatabaseAtStartupAsync(
        this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var database = scope.ServiceProvider
            .GetRequiredService<PayrollDbContext>();

        var configuration = scope.ServiceProvider
            .GetRequiredService<DatabaseConfiguration>();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("PayrollEngine.DatabaseStartupCheck");

        try
        {
            if (await database.Database.CanConnectAsync())
            {
                logger.LogInformation(
                    "Az SQL-kapcsolat sikeres. Adatbázis: {DatabaseName}",
                    configuration.DatabaseName);
                return;
            }

            logger.LogWarning(
                "Az SQL-adatbázis nem érhető el induláskor: {DatabaseName}",
                configuration.DatabaseName);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Az SQL-adatbázis nem érhető el induláskor: {DatabaseName}",
                configuration.DatabaseName);
        }
    }
}