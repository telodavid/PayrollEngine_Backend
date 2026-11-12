namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record WorkScheduleDayModel(
    int DayOfPeriod,
    int DayTypeId,
    string Code,
    string Name,
    bool IsWorkday,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    short BreakMinutes);