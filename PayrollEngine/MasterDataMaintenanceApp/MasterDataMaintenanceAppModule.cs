using PayrollEngine.Domain.Apps;

namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed class MasterDataMaintenanceAppModule : IAppModule
{
    public string Code => "MASTERDATA_MAINTENANCE";

    public string AppName => "Törzsadat-karbantartó alkalmazás";
}