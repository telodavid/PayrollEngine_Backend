using PayrollEngine.Domain.Apps;

namespace PayrollEngine.MasterDataTypeMaintenanceApp;

public sealed class MasterDataTypeMaintenanceAppModule : IAppModule
{
    public string Code => "MASTERDATA_TYPE_MAINTENANCE";

    public string AppName => "Törzsadattípus-karbantartó alkalmazás";
}