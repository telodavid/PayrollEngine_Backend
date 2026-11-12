using System.Data;
using Microsoft.EntityFrameworkCore;

namespace PayrollEngine.UserMaintenanceApp;

public sealed class RoleAssignMaintenanceRepository(
    PayrollDbContext database)
{
    public async Task<IReadOnlyList<UserRoleAssignmentSummary>>
    GetAssignmentsForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_RoleToUser_GetForMaintenance";
            command.CommandType = CommandType.StoredProcedure;

            var userIdParameter = command.CreateParameter();
            userIdParameter.ParameterName = "@UserId";
            userIdParameter.Value = userId;
            command.Parameters.Add(userIdParameter);

            var assignments = new List<UserRoleAssignmentSummary>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var validToOrdinal = reader.GetOrdinal("ValidTo");

                assignments.Add(new UserRoleAssignmentSummary(
                    reader.GetInt32(reader.GetOrdinal("UserId")),
                    reader.GetInt32(reader.GetOrdinal("RoleId")),
                    reader.GetString(reader.GetOrdinal("Code")),
                    reader.GetString(reader.GetOrdinal("RoleName")),
                    reader.GetBoolean(reader.GetOrdinal("RoleIsActive")),
                    DateOnly.FromDateTime(
                        reader.GetDateTime(reader.GetOrdinal("ValidFrom"))),
                    reader.IsDBNull(validToOrdinal)
                        ? null
                        : DateOnly.FromDateTime(reader.GetDateTime(validToOrdinal))));
            }

            return assignments;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
    public async Task DeleteRoleAssignmentAsync(
    int userId,
    int roleId,
    DateOnly validFrom,
    CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(userId),
                "Érvénytelen felhasználó- vagy szerepazonosító.");
        }

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_RoleToUser_Delete";
            command.CommandType = CommandType.StoredProcedure;

            var userIdParameter = command.CreateParameter();
            userIdParameter.ParameterName = "@UserId";
            userIdParameter.Value = userId;
            command.Parameters.Add(userIdParameter);

            var roleIdParameter = command.CreateParameter();
            roleIdParameter.ParameterName = "@RoleId";
            roleIdParameter.Value = roleId;
            command.Parameters.Add(roleIdParameter);

            var validFromParameter = command.CreateParameter();
            validFromParameter.ParameterName = "@ValidFrom";
            validFromParameter.DbType = DbType.Date;
            validFromParameter.Value = validFrom.ToDateTime(TimeOnly.MinValue);
            command.Parameters.Add(validFromParameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
    public async Task AssignRoleToUserAsync(
        int userId,
        int roleId,
        DateOnly validFrom,
        DateOnly? validTo,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        if (roleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roleId));
        }

        if (validTo.HasValue && validTo.Value < validFrom)
        {
            throw new ArgumentException(
                "Az érvényesség vége nem lehet korábbi a kezdeténél.",
                nameof(validTo));
        }

        var connection = database.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "dbo.usp_RoleToUser_Assign";
            command.CommandType = CommandType.StoredProcedure;

            var userIdParameter = command.CreateParameter();
            userIdParameter.ParameterName = "@UserId";
            userIdParameter.Value = userId;
            command.Parameters.Add(userIdParameter);

            var roleIdParameter = command.CreateParameter();
            roleIdParameter.ParameterName = "@RoleId";
            roleIdParameter.Value = roleId;
            command.Parameters.Add(roleIdParameter);

            var validFromParameter = command.CreateParameter();
            validFromParameter.ParameterName = "@ValidFrom";
            validFromParameter.DbType = DbType.Date;
            validFromParameter.Value =
                validFrom.ToDateTime(TimeOnly.MinValue);
            command.Parameters.Add(validFromParameter);

            var validToParameter = command.CreateParameter();
            validToParameter.ParameterName = "@ValidTo";
            validToParameter.DbType = DbType.Date;
            validToParameter.Value = validTo.HasValue
                ? validTo.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;
            command.Parameters.Add(validToParameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
public sealed record UserRoleAssignmentSummary(
    int UserId,
    int RoleId,
    string Code,
    string RoleName,
    bool RoleIsActive,
    DateOnly ValidFrom,
    DateOnly? ValidTo);