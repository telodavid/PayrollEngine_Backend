using Microsoft.EntityFrameworkCore;
using PayrollEngine.RoleCheckAndLogApp;
using PayrollEngine.AppCatalog;
using PayrollEngine.Domain.Apps;
using PayrollEngine.UserMaintenanceApp;
using PayrollEngine.MasterDataMaintenanceApp;
using PayrollEngine.WorkScheduleConfigApp;
using PayrollEngine.AbsenceTypeMaintenanceApp;
using PayrollEngine.MasterDataTypeMaintenanceApp;
using PayrollEngine.PayrollElementMaintenanceApp;
using PayrollEngine.CalculationRuleMaintenanceApp;
using PayrollEngine.PayrollSchemaMaintenanceApp;
using PayrollEngine.PayrollRunApp;
using PayrollEngine.ReportBuilderApp;

namespace PayrollEngine;

public static class DatabaseRegistration
{
    public static IServiceCollection AddPayrollDatabase(
        this IServiceCollection services)
    {
        var configPath = Path.Combine(
            AppContext.BaseDirectory,
            "DatabaseConfig.txt");

        var databaseConfiguration =
            DatabaseConfiguration.Load(configPath);

        services.AddSingleton(databaseConfiguration);

        services.AddDbContext<PayrollDbContext>(options =>
            options.UseSqlServer(
                databaseConfiguration.BuildConnectionString()));

        services.AddScoped<UserAccessRepository>();

        services.AddScoped<UserAccessRepository>();

        services.AddSingleton<AppModuleCatalog>();

        services.AddSingleton<IAppModule, UserMaintenanceAppModule>();
        services.AddSingleton<IAppModule, MasterDataMaintenanceAppModule>();
        services.AddSingleton<IAppModule, WorkScheduleConfigAppModule>();
        services.AddSingleton<IAppModule, AbsenceTypeMaintenanceAppModule>();
        services.AddSingleton<IAppModule, MasterDataTypeMaintenanceAppModule>();
        services.AddSingleton<IAppModule, PayrollElementMaintenanceAppModule>();
        services.AddSingleton<IAppModule, CalculationRuleMaintenanceAppModule>();
        services.AddSingleton<IAppModule, PayrollSchemaMaintenanceAppModule>();
        services.AddSingleton<IAppModule, PayrollRunAppModule>();
        services.AddSingleton<IAppModule, ReportBuilderAppModule>();

        services.AddScoped<UserMaintenanceRepository>();

        return services;
    }
}