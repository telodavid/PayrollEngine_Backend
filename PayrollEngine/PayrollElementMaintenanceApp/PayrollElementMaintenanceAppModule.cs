using PayrollEngine.Domain.Apps;

namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed class PayrollElementMaintenanceAppModule : IAppModule
{
    public string Code => "PAYROLL_ELEMENT_MAINTENANCE";

    public string AppName => "Bérelemek karbantartó alkalmazás";
}