namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record EmployeeDetails(
    int EmployeeId,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    bool IsActive,
    string? PersonalId,
    string? TaxId,
    string? TAJId,
    string? Address,
    DateOnly? PayrollRunFrom,
    DateOnly? JoinDate);