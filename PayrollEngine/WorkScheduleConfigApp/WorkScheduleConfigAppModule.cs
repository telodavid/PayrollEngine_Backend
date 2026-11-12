using PayrollEngine.Domain.Apps;

namespace PayrollEngine.WorkScheduleConfigApp;

public sealed class WorkScheduleConfigAppModule : IAppModule
{
    public string Code => "WORKSCHEDULE_CONFIG";

    public string AppName => "Munkarend-konfiguráló alkalmazás";
}