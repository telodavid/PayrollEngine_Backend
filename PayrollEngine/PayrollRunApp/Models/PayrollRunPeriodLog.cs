using PayrollEngine.CalculationRuleMaintenanceApp.Execution;

namespace PayrollEngine.PayrollRunApp.Models;


public sealed class PayrollRunPeriodLog
{
    public string Period { get; init; } = string.Empty;

    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }

    public List<PayrollRunRuleLog> Rules { get; init; } = [];
    public List<PayrollRunElementLog> PayrollElements { get; init; } = [];

    public List<WorkCalendarDay> WorkCalendar { get; set; } = [];

    public List<WorkCalendarEntry> CalendarEntries { get; set; } = [];
}