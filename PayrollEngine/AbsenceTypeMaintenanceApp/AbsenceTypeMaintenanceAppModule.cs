using PayrollEngine.Domain.Apps;

namespace PayrollEngine.AbsenceTypeMaintenanceApp;

public sealed class AbsenceTypeMaintenanceAppModule : IAppModule
{
    public string Code => "ABSENCE_TYPE_MAINTENANCE";

    public string AppName => "Távolléttípus-karbantartó alkalmazás";
}