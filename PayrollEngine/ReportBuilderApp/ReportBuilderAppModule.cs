using PayrollEngine.Domain.Apps;

namespace PayrollEngine.ReportBuilderApp;

public sealed class ReportBuilderAppModule : IAppModule
{
    public string Code => "REPORT_BUILDER";

    public string AppName => "Riportkészítő alkalmazás";
}