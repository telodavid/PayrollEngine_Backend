using PayrollEngine.Domain.Apps;

namespace PayrollEngine.UserMaintenanceApp;

public sealed class UserMaintenanceAppModule : IAppModule
{
    public string Code => "USER_MAINTENANCE";

    public string AppName => "User karbantartó alkalmazás";
}