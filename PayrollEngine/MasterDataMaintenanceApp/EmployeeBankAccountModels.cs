namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record SetEmployeeBankAccountRequest(
    string AccountType,
    string AccountIdentifier,
    DateOnly ValidFrom);

public sealed record EmployeeBankAccountChangeResult(
    int EmployeeBankAccountId,
    int EmployeeId,
    string AccountType,
    string AccountIdentifier,
    DateOnly ValidFrom,
    DateOnly? ValidTo);
public sealed record EmployeeBankAccountListItem(
    int EmployeeBankAccountId,
    int EmployeeId,
    string AccountType,
    string AccountIdentifier,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record EmployeeBankAccountDeleteResult(
    int EmployeeId,
    int EmployeeBankAccountId);