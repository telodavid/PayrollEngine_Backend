namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed record PayrollElementFieldGroupModel(
    int MasterDataGroupId,
    string Code,
    string Name,
    IReadOnlyList<PayrollElementFieldModel> Fields);