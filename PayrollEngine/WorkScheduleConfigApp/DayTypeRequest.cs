namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record DayTypeRequest(
    string Code,
    string Name,
    bool IsWorkday,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    short BreakMinutes);