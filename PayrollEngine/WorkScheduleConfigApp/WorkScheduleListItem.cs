namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record WorkScheduleListItem(
    int WorkScheduleId,
    string Name,
    int PeriodLengthDays,
    bool IsActive,
    int ConfiguredDays);