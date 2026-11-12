namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record CreateWorkScheduleRequest(
    string Name,
    int PeriodLengthDays,
    IReadOnlyList<WorkScheduleDayRequest> Days);