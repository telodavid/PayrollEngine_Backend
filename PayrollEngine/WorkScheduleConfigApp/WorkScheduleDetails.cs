namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record WorkScheduleDetails(
    int WorkScheduleId,
    string Name,
    int PeriodLengthDays,
    bool IsActive,
    IReadOnlyList<WorkScheduleDayModel> Days);