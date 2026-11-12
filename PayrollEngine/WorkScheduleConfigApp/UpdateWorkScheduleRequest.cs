namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record UpdateWorkScheduleRequest(
    string Name,
    int PeriodLengthDays,
    bool IsActive,
    IReadOnlyList<WorkScheduleDayRequest> Days);