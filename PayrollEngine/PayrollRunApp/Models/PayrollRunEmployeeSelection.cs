namespace PayrollEngine.PayrollRunApp.Models;

public sealed record PayrollRunEmployeeSelection(
    int EmployeeId,
    string FirstName,
    string LastName,
    bool IsActive,
    DateOnly? JoinDate,
    DateOnly? PayrollRunFrom,
    DateOnly EffectivePeriodFrom,
    DateOnly? TerminationDate);