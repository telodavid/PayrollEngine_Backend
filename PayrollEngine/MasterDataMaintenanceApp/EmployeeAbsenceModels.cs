namespace PayrollEngine.MasterDataMaintenanceApp;

public sealed record CreateEmployeeAbsenceRequest(
    int AbsenceTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool FullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record EmployeeAbsenceRecord(
    int AbsenceId,
    int EmploymentId,
    int AbsenceTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool FullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime);

public sealed record EmployeeAbsenceListItem(
    int EmployeeId,
    int AbsenceId,
    int EmploymentId,
    int AbsenceTypeId,
    string AbsenceTypeCode,
    string AbsenceTypeName,
    bool IsPaid,
    DateOnly StartDate,
    DateOnly EndDate,
    bool FullDay,
    TimeOnly? StartTime,
    TimeOnly? EndTime);