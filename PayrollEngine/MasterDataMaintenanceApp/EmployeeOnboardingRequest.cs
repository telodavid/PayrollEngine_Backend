namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record EmployeeOnboardingRequest(
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    string? PersonalId,
    string? TaxId,
    string? TAJId,
    string? Address,
    DateOnly? JoinDate,
    int? CompanyId,
    int? WorkScheduleId,
    int? PayrollElementId,
    decimal? BaseSalaryAmount);