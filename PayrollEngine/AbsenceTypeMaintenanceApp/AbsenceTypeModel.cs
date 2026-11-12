namespace PayrollEngine.AbsenceTypeMaintenanceApp;

public sealed record AbsenceTypeModel(
    int AbsenceTypeId,
    string Code,
    string Name,
    bool IsPaid,
    bool IsActive);