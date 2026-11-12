namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record ConstantItem(
    string ConstantId,
    string ConstantName,
    decimal Value,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record CreateConstantRequest(
    string ConstantId,
    string ConstantName,
    decimal Value,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record UpdateConstantRequest(
    string ConstantName,
    decimal Value,
    DateOnly? ValidTo);