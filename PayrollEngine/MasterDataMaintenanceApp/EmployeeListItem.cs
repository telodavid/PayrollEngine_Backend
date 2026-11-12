namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record EmployeeListItem(
    int EmployeeId,
    string FirstName,
    string LastName,
    bool IsActive,
    DateOnly? JoinDate,
    DateOnly? PayrollRunFrom);