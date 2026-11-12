namespace PayrollEngine.AbsenceTypeMaintenanceApp;

public sealed record CreateAbsenceTypeRequest(
    string Code,
    string Name,
    bool IsPaid);