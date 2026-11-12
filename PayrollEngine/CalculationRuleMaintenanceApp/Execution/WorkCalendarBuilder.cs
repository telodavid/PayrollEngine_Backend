using System.Collections;
using System.Globalization;

namespace PayrollEngine.CalculationRuleMaintenanceApp.Execution;

public sealed record WorkCalendarDay(
    DateOnly Date,
    int DayOfWeek,
    string DayName,
    string? DayTypeCode,
    string? DayTypeName,
    bool? IsWorkday,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    int? BreakMinutes,
    decimal? ScheduledHours)
{
    public IReadOnlyDictionary<string, object?> ToRuleRow()
        => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PAYROLL_RUN.WorkCalendar.Date"] = Date,
            ["PAYROLL_RUN.WorkCalendar.DayOfWeek"] = DayOfWeek,
            ["PAYROLL_RUN.WorkCalendar.DayName"] = DayName,
            ["PAYROLL_RUN.WorkCalendar.DayTypeCode"] = DayTypeCode,
            ["PAYROLL_RUN.WorkCalendar.DayTypeName"] = DayTypeName,
            ["PAYROLL_RUN.WorkCalendar.IsWorkday"] = IsWorkday,
            ["PAYROLL_RUN.WorkCalendar.StartTime"] = StartTime,
            ["PAYROLL_RUN.WorkCalendar.EndTime"] = EndTime,
            ["PAYROLL_RUN.WorkCalendar.BreakMinutes"] = BreakMinutes,
            ["PAYROLL_RUN.WorkCalendar.ScheduledHours"] = ScheduledHours
        };
}

public sealed record WorkCalendarEntry(
    DateOnly Date,
    int DayOfWeek,
    string DayName,
    string? DayTypeCode,
    string? DayTypeName,
    bool? IsWorkday,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    int? BreakMinutes,
    decimal? ScheduledHours,
    string? TypeCode,
    string? TypeName,
    bool? IsPaid,
    bool? FullDay,
    TimeSpan? TypeStartTime,
    TimeSpan? TypeEndTime,
    decimal? EventHours)
{
    public IReadOnlyDictionary<string, object?> ToRuleRow()
        => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PAYROLL_RUN.CalendarEntries.Date"] = Date,
            ["PAYROLL_RUN.CalendarEntries.DayOfWeek"] = DayOfWeek,
            ["PAYROLL_RUN.CalendarEntries.DayName"] = DayName,
            ["PAYROLL_RUN.CalendarEntries.DayTypeCode"] = DayTypeCode,
            ["PAYROLL_RUN.CalendarEntries.DayTypeName"] = DayTypeName,
            ["PAYROLL_RUN.CalendarEntries.IsWorkday"] = IsWorkday,
            ["PAYROLL_RUN.CalendarEntries.StartTime"] = StartTime,
            ["PAYROLL_RUN.CalendarEntries.EndTime"] = EndTime,
            ["PAYROLL_RUN.CalendarEntries.BreakMinutes"] = BreakMinutes,
            ["PAYROLL_RUN.CalendarEntries.ScheduledHours"] = ScheduledHours,
            ["PAYROLL_RUN.CalendarEntries.TypeCode"] = TypeCode,
            ["PAYROLL_RUN.CalendarEntries.TypeName"] = TypeName,
            ["PAYROLL_RUN.CalendarEntries.IsPaid"] = IsPaid,
            ["PAYROLL_RUN.CalendarEntries.FullDay"] = FullDay,
            ["PAYROLL_RUN.CalendarEntries.TypeStartTime"] = TypeStartTime,
            ["PAYROLL_RUN.CalendarEntries.TypeEndTime"] = TypeEndTime,
            ["PAYROLL_RUN.CalendarEntries.EventHours"] = EventHours
        };
}

internal static class WorkCalendarBuilder
{
    private const string ScheduleAssignments =
        "employee.employments.workSchedules";

    private const string CycleDays =
        "workSchedule.days";

    public static IReadOnlyList<WorkCalendarDay> Build(
        IReadOnlyDictionary<string, object?> data,
        DateOnly periodStart)
    {
        var periodEnd = new DateOnly(
            periodStart.Year,
            periodStart.Month,
            DateTime.DaysInMonth(periodStart.Year, periodStart.Month));

        var assignments = Rows(Get(data, ScheduleAssignments)).ToArray();
        var cycleDays = Rows(Get(data, CycleDays)).ToArray();
        var result = new List<WorkCalendarDay>(periodEnd.Day);

        for (var date = periodStart; date <= periodEnd; date = date.AddDays(1))
        {
            var assignment = assignments
                .Where(row => IsValidOn(row, date))
                .OrderByDescending(row =>
                    ReadDate(row, "dbo.EmploymentWorkSchedule.ValidFrom"))
                .ThenByDescending(row =>
                    ReadInt(row, "dbo.EmploymentWorkSchedule.EmploymentWorkScheduleId"))
                .FirstOrDefault();

            var scheduleId = ReadInt(
                assignment,
                "dbo.EmploymentWorkSchedule.WorkScheduleId");

            var cycleStart = ReadDate(
                assignment,
                "dbo.EmploymentWorkSchedule.CycleStartDate");

            var cycleLength = ReadInt(
                assignment,
                "dbo.WorkSchedule.PeriodLengthDays");

            IReadOnlyDictionary<string, object?>? dayType = null;

            if (assignment is not null &&
                scheduleId is > 0 &&
                cycleStart is not null &&
                cycleLength is > 0)
            {
                var offset =
                    (date.DayNumber - cycleStart.Value.DayNumber) %
                    cycleLength.Value;

                if (offset < 0)
                {
                    offset += cycleLength.Value;
                }

                var dayOfPeriod = offset + 1;

                dayType = cycleDays.FirstOrDefault(row =>
                    ReadInt(row, "dbo.WorkScheduleDay.WorkScheduleId") ==
                        scheduleId &&
                    ReadInt(row, "dbo.WorkScheduleDay.DayOfPeriod") ==
                        dayOfPeriod);
            }

            var startTime = ReadTime(dayType, "dbo.DayType.StartTime");
            var endTime = ReadTime(dayType, "dbo.DayType.EndTime");
            var breakMinutes = ReadInt(dayType, "dbo.DayType.BreakMinutes");
            var isWorkday = ReadBool(dayType, "dbo.DayType.IsWorkday");

            decimal? scheduledHours = null;

            if (dayType is not null && isWorkday == false)
            {
                scheduledHours = 0m;
            }
            else if (dayType is not null &&
                     startTime is not null &&
                     endTime is not null &&
                     breakMinutes is not null)
            {
                var duration = endTime.Value - startTime.Value;

                if (duration < TimeSpan.Zero)
                {
                    duration += TimeSpan.FromDays(1);
                }

                scheduledHours = Math.Max(
                    0m,
                    (decimal)duration.TotalMinutes - breakMinutes.Value) / 60m;
            }

            result.Add(new WorkCalendarDay(
                date,
                ((int)date.DayOfWeek + 6) % 7 + 1,
                HungarianDayName(date.DayOfWeek),
                ReadText(dayType, "dbo.DayType.Code"),
                ReadText(dayType, "dbo.DayType.Name"),
                isWorkday,
                startTime,
                endTime,
                breakMinutes,
                scheduledHours));
        }

        return result;
    }

    public static IReadOnlyList<WorkCalendarEntry> BuildEntries(
        IReadOnlyDictionary<string, object?> data,
        IReadOnlyList<WorkCalendarDay> calendar)
    {
        var eventRows = Rows(Get(data, "employee.absences")).ToArray();
        var entries = new List<WorkCalendarEntry>();

        foreach (var day in calendar)
        {
            var eventsForDay = eventRows
                .Where(row =>
                {
                    var startDate = ReadDate(
                        row,
                        "dbo.EmployeeAbsence.StartDate");

                    var endDate = ReadDate(
                        row,
                        "dbo.EmployeeAbsence.EndDate");

                    return startDate is not null &&
                           endDate is not null &&
                           startDate.Value <= day.Date &&
                           endDate.Value >= day.Date;
                })
                .ToArray();

            if (eventsForDay.Length == 0)
            {
                entries.Add(CreateEntry(day, null));
                continue;
            }

            foreach (var eventRow in eventsForDay)
            {
                entries.Add(CreateEntry(day, eventRow));
            }
        }

        return entries;
    }

    private static WorkCalendarEntry CreateEntry(
        WorkCalendarDay day,
        IReadOnlyDictionary<string, object?>? eventRow)
        => new(
            day.Date,
            day.DayOfWeek,
            day.DayName,
            day.DayTypeCode,
            day.DayTypeName,
            day.IsWorkday,
            day.StartTime,
            day.EndTime,
            day.BreakMinutes,
            day.ScheduledHours,
            ReadText(eventRow, "dbo.AbsenceType.Code"),
            ReadText(eventRow, "dbo.AbsenceType.Name"),
            ReadBool(eventRow, "dbo.AbsenceType.IsPaid"),
            ReadBool(eventRow, "dbo.EmployeeAbsence.FullDay"),
            ReadTime(eventRow, "dbo.EmployeeAbsence.StartTime"),
            ReadTime(eventRow, "dbo.EmployeeAbsence.EndTime"),
            CalculateEventHours(day, eventRow));

    private static decimal? CalculateEventHours(
        WorkCalendarDay day,
        IReadOnlyDictionary<string, object?>? eventRow)
    {
        if (eventRow is null)
        {
            return 0m;
        }

        if (ReadBool(eventRow, "dbo.EmployeeAbsence.FullDay") == true)
        {
            return day.ScheduledHours;
        }

        var eventStartDate = ReadDate(
            eventRow,
            "dbo.EmployeeAbsence.StartDate");

        var eventEndDate = ReadDate(
            eventRow,
            "dbo.EmployeeAbsence.EndDate");

        if (eventStartDate is null ||
            eventEndDate is null ||
            day.StartTime is null ||
            day.EndTime is null ||
            day.Date < eventStartDate.Value ||
            day.Date > eventEndDate.Value)
        {
            return 0m;
        }

        var eventStartTime = day.Date == eventStartDate.Value
            ? ReadTime(eventRow, "dbo.EmployeeAbsence.StartTime")
            : TimeSpan.Zero;

        var eventEndTime = day.Date == eventEndDate.Value
            ? ReadTime(eventRow, "dbo.EmployeeAbsence.EndTime")
            : TimeSpan.FromDays(1);

        if (eventStartTime is null || eventEndTime is null)
        {
            return null;
        }

        var shiftStart = (decimal)day.StartTime.Value.TotalMinutes;
        var shiftEnd = (decimal)day.EndTime.Value.TotalMinutes;

        if (shiftEnd < shiftStart)
        {
            shiftEnd += 24m * 60m;
        }

        var absenceStart = (decimal)eventStartTime.Value.TotalMinutes;
        var absenceEnd = (decimal)eventEndTime.Value.TotalMinutes;

        if (eventStartDate == eventEndDate && absenceEnd < absenceStart)
        {
            absenceEnd += 24m * 60m;
        }

        var overlap = Math.Max(
            0m,
            Math.Min(shiftEnd, absenceEnd) -
            Math.Max(shiftStart, absenceStart));

        var overlapHours = overlap / 60m;

        return day.ScheduledHours is { } scheduledHours
            ? Math.Min(overlapHours, scheduledHours)
            : overlapHours;
    }

    private static bool IsValidOn(
        IReadOnlyDictionary<string, object?> row,
        DateOnly date)
    {
        var from = ReadDate(
            row,
            "dbo.EmploymentWorkSchedule.ValidFrom");

        var to = ReadDate(
            row,
            "dbo.EmploymentWorkSchedule.ValidTo");

        return from is not null &&
               from.Value <= date &&
               (to is null || to.Value >= date);
    }

    private static object? Get(
        IReadOnlyDictionary<string, object?> data,
        string key)
        => data.TryGetValue(key, out var value) ? value : null;

    private static IEnumerable<IReadOnlyDictionary<string, object?>> Rows(
        object? value)
    {
        if (value is not IEnumerable enumerable || value is string)
        {
            yield break;
        }

        foreach (var item in enumerable)
        {
            if (item is IReadOnlyDictionary<string, object?> row)
            {
                yield return row;
            }
        }
    }

    private static object? Field(
        IReadOnlyDictionary<string, object?>? row,
        string key)
    {
        if (row is null)
        {
            return null;
        }

        return row.TryGetValue(key, out var value) ? value : null;
    }

    private static DateOnly? ReadDate(
        IReadOnlyDictionary<string, object?>? row,
        string key)
    {
        var value = Field(row, key);

        return value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            string text when DateOnly.TryParse(
                text,
                CultureInfo.InvariantCulture,
                out var date) => date,
            _ => null
        };
    }

    private static TimeSpan? ReadTime(
        IReadOnlyDictionary<string, object?>? row,
        string key)
    {
        var value = Field(row, key);

        return value switch
        {
            TimeSpan time => time,
            DateTime dateTime => dateTime.TimeOfDay,
            string text when TimeSpan.TryParse(
                text,
                CultureInfo.InvariantCulture,
                out var time) => time,
            _ => null
        };
    }

    private static int? ReadInt(
        IReadOnlyDictionary<string, object?>? row,
        string key)
    {
        var value = Field(row, key);

        return value switch
        {
            int number => number,
            short number => number,
            long number when number is >= int.MinValue and <= int.MaxValue
                => (int)number,
            string text when int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number) => number,
            _ => null
        };
    }

    private static bool? ReadBool(
        IReadOnlyDictionary<string, object?>? row,
        string key)
    {
        var value = Field(row, key);

        return value switch
        {
            bool boolean => boolean,
            byte number => number != 0,
            int number => number != 0,
            string text when bool.TryParse(text, out var boolean) => boolean,
            _ => null
        };
    }

    private static string? ReadText(
        IReadOnlyDictionary<string, object?>? row,
        string key)
        => Field(row, key)?.ToString();

    private static string HungarianDayName(DayOfWeek dayOfWeek)
        => dayOfWeek switch
        {
            DayOfWeek.Monday => "hétfő",
            DayOfWeek.Tuesday => "kedd",
            DayOfWeek.Wednesday => "szerda",
            DayOfWeek.Thursday => "csütörtök",
            DayOfWeek.Friday => "péntek",
            DayOfWeek.Saturday => "szombat",
            _ => "vasárnap"
        };
}