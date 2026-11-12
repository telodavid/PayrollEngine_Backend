namespace PayrollEngine.WorkScheduleConfigApp;

public sealed record WorkScheduleDayRequest(
    int DayOfPeriod,
    int DayTypeId);