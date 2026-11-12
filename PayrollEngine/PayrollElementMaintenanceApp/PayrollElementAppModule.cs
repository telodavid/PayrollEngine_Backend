using PayrollEngine.Domain.Apps;

namespace PayrollEngine.PayrollElementMaintenanceApp;

public sealed class PayrollElementAppModule : IAppModule
{
    public string Code => "PAYROLL_ELEMENT_MAINTENANCE";

    public string AppName => "Bérelemek karbantartó alkalmazás";
}