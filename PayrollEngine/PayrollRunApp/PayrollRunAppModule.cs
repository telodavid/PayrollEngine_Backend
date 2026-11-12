using PayrollEngine.Domain.Apps;

namespace PayrollEngine.PayrollRunApp;

public sealed class PayrollRunAppModule : IAppModule
{
    public string Code => "PAYROLL_RUN";

    public string AppName => "Számfejtő alkalmazás";
}