using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PayrollEngine;
using PayrollEngine.AbsenceTypeMaintenanceApp;
using PayrollEngine.AppCatalog;
using PayrollEngine.AuthorizationAndAuditApp;
using PayrollEngine.CalculationRuleMaintenanceApp;
using PayrollEngine.CompanyMaintenanceApp;
using PayrollEngine.Domain.Apps;
using PayrollEngine.LoginApp;
using PayrollEngine.MasterDataMaintenanceApp;
using PayrollEngine.MasterDataTypeMaintenanceApp;
using PayrollEngine.PayrollElementMaintenanceApp;
using PayrollEngine.PayrollSchemaMaintenanceApp;
using PayrollEngine.UserMaintenanceApp;
using PayrollEngine.WorkScheduleConfigApp;
using PayrollEngine.PayrollRunApp;
using PayrollEngine.CalculationRuleMaintenanceApp.Execution;
using PayrollEngine.ReportBuilderApp;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.StaticFiles;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("PayrollFrontend", policy =>
    {
        policy
            .WithOrigins("https://localhost:7279")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddPayrollDatabase();
builder.Services.AddScoped<UserLoginRepository>();
builder.Services.AddLoginAuthentication();
builder.Services.AddScoped<RoleAssignMaintenanceRepository>();
builder.Services.AddScoped<RoleMaintenanceRepository>();
builder.Services.AddScoped<AppMaintenanceRepository>();
builder.Services.AddScoped<AppToRoleMaintenanceRepository>();
builder.Services.AddScoped<MdgRoleMaintenanceRepository>();
builder.Services.AddSingleton<IAuditLogWriter, FileAuditLogWriter>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<UserMaintenanceRepository>();
builder.Services.AddScoped<DayTypeRepository>();
builder.Services.AddScoped<WorkScheduleRepository>();
builder.Services.AddSingleton<IAppModule, UserMaintenanceAppModule>();
builder.Services.AddSingleton<IAppModule, WorkScheduleConfigAppModule>();
builder.Services.AddSingleton<AppModuleCatalog>();
builder.Services.AddScoped<AbsenceTypeRepository>();
builder.Services.AddSingleton<IAppModule, AbsenceTypeMaintenanceAppModule>();
builder.Services.AddScoped<MasterDataFieldRepository>();
builder.Services.AddScoped<MasterDataFieldOptionRepository>();
builder.Services.AddScoped<MasterDataFieldLookupRepository>();
builder.Services.AddSingleton<IAppModule, MasterDataTypeMaintenanceAppModule>();
builder.Services.AddScoped<EmployeeOnboardingRepository>();
builder.Services.AddSingleton<IAppModule, MasterDataMaintenanceAppModule>();
builder.Services.AddScoped<EmployeeMaintenanceRepository>();
builder.Services.AddScoped<EmployeeQueryRepository>();
builder.Services.AddScoped<EmployeeWorkScheduleChangeRepository>();
builder.Services.AddScoped<EmploymentMaintenanceRepository>();
builder.Services.AddScoped<CompanyMaintenanceRepository>();
builder.Services.AddScoped<EmployeeAbsenceRepository>();
builder.Services.AddScoped<EmployeePayrollElementRepository>();
builder.Services.AddScoped<EmployeeBankAccountRepository>();
builder.Services.AddScoped<EmployeeTerminationRepository>();
builder.Services.AddScoped<MasterDataGroupAccessRepository>();
builder.Services.AddScoped<EmployeeFieldValueRepository>();
builder.Services.AddScoped<PayrollElementMaintenanceRepository>();
builder.Services.AddSingleton<IAppModule, PayrollElementAppModule>();
builder.Services.AddSingleton<OperationCatalogService>();
builder.Services.AddSingleton<IAppModule, CalculationRuleMaintenanceAppModule>();
builder.Services.AddSingleton<RuleFileStorageService>();
builder.Services.AddScoped<DatabaseSchemaRepository>();
builder.Services.AddScoped<PayrollDataCatalogService>();
builder.Services.AddScoped<DynamicFieldMetadataRepository>();
builder.Services.AddScoped<RuleDefinitionValidator>();
builder.Services.AddScoped<PayrollSchemaFileStorageService>();
builder.Services.AddSingleton<IAppModule, PayrollSchemaMaintenanceAppModule>();
builder.Services.AddScoped<PayrollRunEmployeeSelectionRepository>();
builder.Services.AddSingleton<OperationEvaluator>();
builder.Services.AddScoped<PayrollSchemaRuleExecutor>();
builder.Services.AddScoped<PayrollRunDataRepository>();
builder.Services.AddScoped<PayrollRunExecutionService>();
builder.Services.AddSingleton<PayrollRunLogFileStorageService>();
builder.Services.AddScoped<PayrollRunResultRepository>();
builder.Services.AddScoped<PayrollRunPreviousResultsRepository>();
builder.Services.AddScoped<ReportDefinitionFileStorageService>();
builder.Services.AddScoped<ReportDataRepository>();
builder.Services.AddSingleton<ReportOutputRowBuilder>();
builder.Services.AddSingleton<ReportFileExportService>();
builder.Services.AddSingleton<ReportOfficeExportService>();
builder.Services.AddScoped<MasterDataGroupMaintenanceRepository>();
builder.Services.AddScoped<EmployeeDependentRepository>();
builder.Services.AddScoped<ConstantMaintenanceRepository>();
builder.Services.AddScoped<ReportDatabaseMetadataRepository>();
builder.Services.AddScoped<ReportDatabaseCatalogBuilder>();
builder.Services.AddSingleton<ReportDataCatalogMerger>();
builder.Services.AddScoped<ReportDataCatalogService>();
builder.Services.AddScoped<ReportParameterValueResolver>();
builder.Services.AddScoped<ReportDataQueryBuilder>();
builder.Services.AddScoped<ReportEmployeeDocumentBuilder>();
builder.Services.AddSingleton<ReportEmployeeDocumentExportService>();
builder.Services.AddScoped<ReportEmployeeDocumentFileExportService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var frontendIndex = Path.Combine(
    app.Environment.WebRootPath ?? "wwwroot",
    "index.html");

if (File.Exists(frontendIndex))
{
    app.UseDefaultFiles();

    var contentTypeProvider = new FileExtensionContentTypeProvider();
    contentTypeProvider.Mappings[".dat"] = "application/octet-stream";

    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = contentTypeProvider
    });
}

app.UseCors("PayrollFrontend");
app.UseLoginAuthentication();

app.MapDatabaseHealthEndpoint();
app.MapLoginEndpoints();
app.MapUserMaintenanceEndpoints();
app.MapRoleMaintenanceEndpoints();
app.MapRoleAssignMaintenanceEndpoints();
app.MapAppMaintenanceEndpoints();
app.MapAppToRoleMaintenanceEndpoints();
app.MapMdgRoleMaintenanceEndpoints();
app.MapDayTypeEndpoints();
app.MapWorkScheduleEndpoints();
app.MapAbsenceTypeEndpoints();
app.MapMasterDataFieldEndpoints();
app.MapMasterDataFieldOptionEndpoints();
app.MapMasterDataFieldLookupEndpoints();
app.MapEmployeeOnboardingEndpoints();
app.MapEmployeeMaintenanceEndpoints();
app.MapEmployeeQueryEndpoints();
app.MapEmployeeWorkScheduleChangeEndpoints();
app.MapEmploymentMaintenanceEndpoints();
app.MapCompanyMaintenanceEndpoints();
app.MapEmployeeAbsenceEndpoints();
app.MapEmployeeAbsenceDeleteEndpoints();
app.MapEmployeeAbsenceQueryEndpoints();
app.MapEmployeePayrollElementEndpoints();
app.MapEmployeeBankAccountEndpoints();
app.MapEmployeeTerminationEndpoints();
app.MapMasterDataGroupAccessEndpoints();
app.MapEmployeeFieldValueEndpoints();
app.MapPayrollElementMaintenanceEndpoints();
app.MapPayrollElementFieldValueEndpoints();
app.MapOperationCatalogEndpoints();
app.MapRuleMaintenanceEndpoints();
app.MapDatabaseSchemaCatalogEndpoints();
app.MapPayrollSchemaEndpoints();
app.MapPayrollRunEndpoints();
app.MapReportDefinitionEndpoints();
app.MapReportDataCatalogEndpoints();
app.MapReportExportEndpoints();
app.MapMasterDataGroupMaintenanceEndpoints();
app.MapEmployeeDependentEndpoints();
app.MapConstantMaintenanceEndpoints();

if (File.Exists(frontendIndex))
{
    app.MapFallbackToFile("index.html");
}

await app.CheckDatabaseAtStartupAsync();

await app.RunAsync();