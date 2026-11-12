namespace PayrollEngine.PayrollRunApp.Models;

public sealed class PayrollRunEmployeeLog
{
    public int EmployeeId { get; init; }
    public string? EmployeeName { get; init; }

    public DateOnly? JoinDate { get; init; }
    public DateOnly? PayrollRunFromBefore { get; init; }
    public DateOnly? PayrollRunFromAfter { get; set; }

    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }

    public List<PayrollRunPeriodLog> Periods { get; init; } = [];
}