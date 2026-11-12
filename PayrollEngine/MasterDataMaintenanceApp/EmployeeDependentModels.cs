namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record EmployeeDependentItem(
    int EmployeeDependentId,
    int EmployeeId,
    int DependentId,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? TaxId,
    int RelationshipTypeId,
    string RelationshipTypeName,
    DateOnly? ValidFrom,
    DateOnly? ValidTo);

public sealed record RelationshipTypeItem(
    int RelationshipTypeId,
    string Name);

public sealed record SaveEmployeeDependentRequest(
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? TaxId,
    int RelationshipTypeId,
    DateOnly? ValidFrom,
    DateOnly? ValidTo);

public sealed record EmployeeDependentSaveResult(
    int EmployeeDependentId,
    int EmployeeId,
    int DependentId,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? TaxId,
    int RelationshipTypeId,
    string RelationshipTypeName,
    DateOnly? ValidFrom,
    DateOnly? ValidTo);