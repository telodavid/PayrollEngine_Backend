namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record ChangeEmployeeWorkScheduleRequest(
    int WorkScheduleId,
    DateOnly ValidFrom,
    DateOnly CycleStartDate,
    DateOnly? RetroPayrollRunFrom);

public sealed record EmployeeWorkScheduleChangeResult(
    bool RequiresRetroPayroll,
    DateOnly? SuggestedPayrollRunFrom,
    int? EmployeeId,
    int? WorkScheduleId,
    DateOnly? ValidFrom,
    DateOnly? CycleStartDate,
    string Message);

public sealed record EmployeeWorkScheduleAssignmentListItem(
    int EmploymentWorkScheduleId,
    int WorkScheduleId,
    string WorkScheduleName,
    int PeriodLengthDays,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateOnly CycleStartDate);

public sealed record EmployeeWorkScheduleDeleteResult(
    int EmployeeId,
    int EmploymentWorkScheduleId);