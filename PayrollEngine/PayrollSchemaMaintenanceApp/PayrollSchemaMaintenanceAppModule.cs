using PayrollEngine.Domain.Apps;

namespace PayrollEngine.PayrollSchemaMaintenanceApp;

public sealed class PayrollSchemaMaintenanceAppModule : IAppModule
{
    public string Code => "PAYROLL_SCHEMA_MAINTENANCE";

    public string AppName => "Séma-karbantartó alkalmazás";
}