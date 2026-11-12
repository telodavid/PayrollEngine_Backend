namespace PayrollEngine.CompanyMaintenanceApp;

public sealed record CompanyModel(
    int CompanyId,
    string CompanyName,
    string? CompanyAddress,
    string? BankAccountNumber,
    string? TaxId,
    string? PhoneNumber);

public sealed record SaveCompanyRequest(
    string CompanyName,
    string? CompanyAddress,
    string? BankAccountNumber,
    string? TaxId,
    string? PhoneNumber);