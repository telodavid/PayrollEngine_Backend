using PayrollEngine.Domain.Apps;
using PayrollEngine.RoleCheckAndLogApp;


namespace PayrollEngine.AppCatalog;

public sealed class AppModuleCatalog
{
    private readonly IReadOnlyList<IAppModule> _modules;

    public AppModuleCatalog(IEnumerable<IAppModule> modules)
    {
        _modules = modules.ToArray();
    }

    public IReadOnlyList<IAppModule> GetAvailableModules(UserAccess userAccess)
    {
        var allowedCodes = userAccess.Apps
            .Select(app => app.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _modules
            .Where(module => allowedCodes.Contains(module.Code))
            .GroupBy(
                module => module.Code,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(module => module.AppName)
            .ToArray();
    }
}