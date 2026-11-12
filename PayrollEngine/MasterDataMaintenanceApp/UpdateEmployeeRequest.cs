namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record UpdateEmployeeRequest(
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    string? PersonalId,
    string? TaxId,
    string? TAJId,
    string? Address);