namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record DayTypeModel(
    int DayTypeId,
    string Code,
    string Name,
    bool IsWorkday,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    short BreakMinutes);