using PayrollEngine.Domain.Apps;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public sealed class CalculationRuleMaintenanceAppModule : IAppModule
{
    public string Code => "CALCULATION_RULE_MAINTENANCE";

    public string AppName => "Számítási szabály karbantartó alkalmazás";
}