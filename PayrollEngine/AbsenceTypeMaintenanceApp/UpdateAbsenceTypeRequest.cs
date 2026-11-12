namespace PayrollEngine.AbsenceTypeMaintenanceApp;

public sealed record UpdateAbsenceTypeRequest(
    string Code,
    string Name,
    bool IsPaid,
    bool IsActive);