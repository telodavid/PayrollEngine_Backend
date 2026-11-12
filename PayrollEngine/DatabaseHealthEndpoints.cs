using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PayrollEngine;

public static class DatabaseHealthEndpoints
{
    public static WebApplication MapDatabaseHealthEndpoint(
        this WebApplication app)
    {
        app.MapGet("/api/health/database", CheckDatabaseHealthAsync)
            .WithName("GetDatabaseHealth");

        return app;
    }

    private static async Task<IResult> CheckDatabaseHealthAsync(
        PayrollDbContext database,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(
            "PayrollEngine.DatabaseHealth");

        try
        {
            if (await database.Database.CanConnectAsync())
            {
                return Results.Ok(new
                {
                    status = "available",
                    message = "SQL szolgáltatás elérhető."
                });
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Az adatbázis-állapot ellenőrzése sikertelen.");
        }

        return Results.Json(
            new
            {
                status = "unavailable",
                message = "SQL szolgáltatás nem érhető el."
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}