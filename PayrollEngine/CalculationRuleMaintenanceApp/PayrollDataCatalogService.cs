using PayrollEngine.CalculationRuleMaintenanceApp.Models;

namespace PayrollEngine.CalculationRuleMaintenanceApp;

public sealed class PayrollDataCatalogService(
    DatabaseSchemaRepository schemaRepository,
    DynamicFieldMetadataRepository dynamicFields)
{
    public async Task<IReadOnlyList<PayrollDataReferenceDefinition>>
        SearchAsync(
            string? search,
            CancellationToken cancellationToken = default)
    {
        var schema = await schemaRepository.GetSchemaAsync(
            cancellationToken);

        var physicalReferences = schema.Columns
            .Select(column =>
                CreatePhysicalReference(column, schema.Relationships));

        // Az összes dinamikus mező kell ahhoz, hogy a gyűjteményeket
        // kereséstől függetlenül is fel tudjuk venni a katalógusba.
        var dynamicMetadata = await dynamicFields.GetAsync(
            null,
            cancellationToken);

        var fieldReferences = physicalReferences
            .Concat(dynamicMetadata.Select(CreateDynamicReference))
            .ToArray();

        var collectionReferences = fieldReferences
            .Where(reference =>
                !string.IsNullOrWhiteSpace(reference.CollectionReference))
            .GroupBy(
                reference => reference.CollectionReference!,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                CreateCollectionReference(group.Key, group.First()));

        var runReferences = new[]
        {
            new PayrollDataReferenceDefinition
            {
                Reference = "PAYROLL_RUN.EmployeeId",
                DisplayName = "Számfejtési futás — munkavállaló-azonosító",
                DataType = "integer",
                Category = "Számfejtési futás",
                Scope = "Számfejtési futás",
                Description =
                    "Az aktuálisan feldolgozott munkavállaló azonosítója."
            },
            new PayrollDataReferenceDefinition
            {
                Reference = "PAYROLL_RUN.Period",
                DisplayName = "Számfejtési futás — periódus kezdete",
                DataType = "date",
                Category = "Számfejtési futás",
                Scope = "Számfejtési futás",
                Description =
                    "Az aktuálisan feldolgozott számfejtési hónap első napja."
            },
            new PayrollDataReferenceDefinition
            {
                Reference = "PAYROLL_RUN.PeriodEnd",
                DisplayName = "Számfejtési futás — periódus vége",
                DataType = "date",
                Category = "Számfejtési futás",
                Scope = "Számfejtési futás",
                Description =
                    "Az aktuálisan feldolgozott számfejtési hónap utolsó napja."
            },
            new PayrollDataReferenceDefinition
            {
                Reference = "PAYROLL_RUN.PeriodYear",
                DisplayName = "Számfejtési futás — periódus éve",
                DataType = "integer",
                Category = "Számfejtési futás",
                Scope = "Számfejtési futás",
                Description =
                    "Az aktuálisan feldolgozott számfejtési periódus éve."
            },
            new PayrollDataReferenceDefinition
            {
                Reference = "PAYROLL_RUN.WorkCalendar",
                DisplayName = "Munkaidő-naptár — napok",
                DataType = "collection",
                Category = "Munkaidő-naptár",
                Scope = "Számfejtési futás",
                Description =
                    "A számfejtési hónap napjai. forEach művelettel dolgozhatók fel."
            },
            WorkCalendarField(
                "Date",
                "Dátum",
                "date",
                "A naptári nap dátuma."),
            WorkCalendarField(
                "DayOfWeek",
                "Hét napja (1–7)",
                "integer",
                "ISO nap-sorszám: hétfő 1, vasárnap 7."),
            WorkCalendarField(
                "DayName",
                "Nap neve",
                "text",
                "A nap magyar neve."),
            WorkCalendarField(
                "DayTypeCode",
                "Naptípus kódja",
                "text",
                "A munkarendhez rendelt naptípus kódja."),
            WorkCalendarField(
                "DayTypeName",
                "Naptípus neve",
                "text",
                "A munkarendhez rendelt naptípus neve."),
            WorkCalendarField(
                "IsWorkday",
                "Munkanap",
                "boolean",
                "A naptípus munkanap-e."),
            WorkCalendarField(
                "StartTime",
                "Kezdő időpont",
                "time",
                "A naptípus kezdő időpontja."),
            WorkCalendarField(
                "EndTime",
                "Záró időpont",
                "time",
                "A naptípus záró időpontja."),
            WorkCalendarField(
                "BreakMinutes",
                "Szünet (perc)",
                "integer",
                "A naptípus szünetének hossza percben."),
            WorkCalendarField(
                "ScheduledHours",
                "Munkarend szerinti órák",
                "decimal",
                "Kezdés és befejezés közötti idő mínusz a szünet."),
            new PayrollDataReferenceDefinition
            {
                Reference = "PAYROLL_RUN.CalendarEntries",
                DisplayName = "Naptári napok és események",
                DataType = "collection",
                Category = "Munkaidő-naptár",
                Scope = "Számfejtési futás",
                Description =
                    "A naptári napok munkarendi adatokkal és az adott napon érvényes eseményekkel. forEach művelettel dolgozhatók fel."
            },
            new PayrollDataReferenceDefinition
            {
                Reference = "PAYROLL_RUN.PayrollElementValues",
                DisplayName = "Bérelemek és értékeik",
                DataType = "collection",
                Category = "Bérelemek",
                Scope = "Számfejtési futás",
                Description =
                    "A hozzárendelt és az eddig kiszámított bérelemek forEach ciklussal feldolgozható listája."
            },
            PayrollElementValuesField(
                "Code",
                "Bérelem kódja",
                "text"),
            PayrollElementValuesField(
                "Amount",
                "Bérelem összege",
                "decimal"),
            PayrollElementValuesField(
                "Source",
                "Érték forrása",
                "text"),
            new PayrollDataReferenceDefinition
            {
                Reference = "PAYROLL_RUN.PreviousResults",
                DisplayName = "Korábbi számfejtési eredmények",
                DataType = "collection",
                Category = "Korábbi számfejtési eredmények",
                Scope = "Számfejtési futás",
                Description =
                    "A korábbi számfejtési időszakok eredményei forEach ciklussal dolgozhatók fel."
            },
            PreviousResultsField(
                "PreviousRunId",
                "Korábbi futás azonosítója",
                "text",
                "A korábbi számfejtési futás azonosítója."),
            PreviousResultsField(
                "Period",
                "Periódus",
                "date",
                "A korábbi eredmény számfejtési periódusa."),
            PreviousResultsField(
                "Year",
                "Periódus éve",
                "integer",
                "A korábbi eredmény számfejtési periódusának éve."),
            PreviousResultsField(
                "PayrollElementId",
                "Bérelem azonosítója",
                "integer",
                "A korábbi eredmény bérelem-azonosítója."),
            PreviousResultsField(
                "PayrollElementCode",
                "Bérelem kódja",
                "text",
                "A korábbi eredmény bérelemkódja."),
            PreviousResultsField(
                "PayrollElementName",
                "Bérelem neve",
                "text",
                "A korábbi eredmény bérelemneve."),
            PreviousResultsField(
                "ResultType",
                "Eredménytípus",
                "text",
                "A korábbi eredmény típusa."),
            PreviousResultsField(
                "Amount",
                "Összeg",
                "decimal",
                "A korábbi számfejtési eredmény összege."),
            CalendarEntryField(
                "Date",
                "Dátum",
                "date",
                "A naptári nap dátuma."),
            CalendarEntryField(
                "DayOfWeek",
                "Hét napja (1–7)",
                "integer",
                "ISO nap-sorszám: hétfő 1, vasárnap 7."),
            CalendarEntryField(
                "DayName",
                "Nap neve",
                "text",
                "A nap magyar neve."),
            CalendarEntryField(
                "DayTypeCode",
                "Naptípus kódja",
                "text",
                "A munkarend ciklusnapjának kódja."),
            CalendarEntryField(
                "DayTypeName",
                "Naptípus neve",
                "text",
                "A munkarend ciklusnapjának neve."),
            CalendarEntryField(
                "IsWorkday",
                "Munkanap",
                "boolean",
                "A naptípus munkanap-e."),
            CalendarEntryField(
                "StartTime",
                "Munkarendi kezdés",
                "time",
                "A munkarendi kezdő időpont."),
            CalendarEntryField(
                "EndTime",
                "Munkarendi befejezés",
                "time",
                "A munkarendi záró időpont."),
            CalendarEntryField(
                "BreakMinutes",
                "Szünet (perc)",
                "integer",
                "A naptípus szünetének hossza."),
            CalendarEntryField(
                "ScheduledHours",
                "Munkarend szerinti órák",
                "decimal",
                "A napi munkarend szerinti órák."),
            CalendarEntryField(
                "TypeCode",
                "Esemény kódja",
                "text",
                "A távollét vagy esemény kódja. Esemény nélkül üres."),
            CalendarEntryField(
                "TypeName",
                "Esemény neve",
                "text",
                "A távollét vagy esemény neve."),
            CalendarEntryField(
                "IsPaid",
                "Fizetett",
                "boolean",
                "A távollét vagy esemény fizetett-e."),
            CalendarEntryField(
                "FullDay",
                "Egész napos",
                "boolean",
                "Egész napos-e az esemény."),
            CalendarEntryField(
                "TypeStartTime",
                "Esemény kezdete",
                "time",
                "Résznapos esemény kezdő időpontja."),
            CalendarEntryField(
                "TypeEndTime",
                "Esemény vége",
                "time",
                "Résznapos esemény záró időpontja."),
            CalendarEntryField(
                "EventHours",
                "Esemény órái az adott napon",
                "decimal",
                "Egész napnál a munkarend szerinti napi órák, résznapnál a munkarenddel átfedő órák.")
        };

        return fieldReferences
            .Concat(runReferences)
            .Concat(collectionReferences)
            .Where(reference => Matches(reference, search))
            .OrderBy(reference => reference.Category)
            .ThenBy(reference => reference.DisplayName)
            .ToArray();
    }

    private static PayrollDataReferenceDefinition WorkCalendarField(
        string field,
        string displayName,
        string dataType,
        string description)
        => new()
        {
            Reference = $"PAYROLL_RUN.WorkCalendar.{field}",
            DisplayName = $"Munkaidő-naptár — {displayName}",
            DataType = dataType,
            Category = "Munkaidő-naptár",
            Scope = "Munkaidő-naptár",
            CollectionReference = "PAYROLL_RUN.WorkCalendar",
            Description = description
        };

    private static PayrollDataReferenceDefinition PayrollElementValuesField(
        string field,
        string displayName,
        string dataType)
        => new()
        {
            Reference = $"PAYROLL_RUN.PayrollElementValues.{field}",
            DisplayName = $"Bérelemek és értékeik — {displayName}",
            DataType = dataType,
            Category = "Bérelemek",
            Scope = "Bérelemek és értékeik",
            CollectionReference = "PAYROLL_RUN.PayrollElementValues",
            Description =
                "A bérelemek és értékeik forEach ciklus aktuális sora."
        };

    private static PayrollDataReferenceDefinition PreviousResultsField(
        string field,
        string displayName,
        string dataType,
        string description)
        => new()
        {
            Reference = $"PAYROLL_RUN.PreviousResults.{field}",
            DisplayName = $"Korábbi számfejtési eredmények — {displayName}",
            DataType = dataType,
            Category = "Korábbi számfejtési eredmények",
            Scope = "Korábbi számfejtési eredmények",
            CollectionReference = "PAYROLL_RUN.PreviousResults",
            Description = description
        };

    private static PayrollDataReferenceDefinition CalendarEntryField(
        string field,
        string displayName,
        string dataType,
        string description)
        => new()
        {
            Reference = $"PAYROLL_RUN.CalendarEntries.{field}",
            DisplayName = $"Naptári nap és esemény — {displayName}",
            DataType = dataType,
            Category = "Munkaidő-naptár",
            Scope = "Naptári nap és esemény",
            CollectionReference = "PAYROLL_RUN.CalendarEntries",
            Description = description
        };

    private static PayrollDataReferenceDefinition CreatePhysicalReference(
        DatabaseColumnMetadata column,
        IReadOnlyList<DatabaseRelationshipMetadata> relationships)
    {
        var relationship = relationships.FirstOrDefault(item =>
            Same(item.SourceSchema, column.SchemaName) &&
            Same(item.SourceTable, column.TableName) &&
            Same(item.SourceColumn, column.ColumnName));

        var tableLabel = GetTableLabel(column.TableName);
        var columnLabel = GetColumnLabel(column.ColumnName);

        var description = relationship is null
            ? $"{column.SchemaName}.{column.TableName}.{column.ColumnName} — SQL-típus: {column.DataType}."
            : $"{column.SchemaName}.{column.TableName}.{column.ColumnName} — " +
              $"kapcsolódik ide: {relationship.TargetSchema}.{relationship.TargetTable}.{relationship.TargetColumn}.";

        return new PayrollDataReferenceDefinition
        {
            Reference =
                $"{column.SchemaName}.{column.TableName}.{column.ColumnName}",
            DisplayName = $"{tableLabel} — {columnLabel}",
            DataType = GetSqlLogicalType(column.DataType),
            Category = tableLabel,
            Scope = GetScope(column.TableName),
            CollectionReference = GetCollectionReference(column.TableName),
            ValueCatalog = relationship is null
                ? null
                : $"{relationship.TargetSchema}.{relationship.TargetTable}",
            Description = description
        };
    }

    private static PayrollDataReferenceDefinition CreateDynamicReference(
        DynamicFieldMetadata field)
    {
        var scopeRoot = GetDynamicScopeRoot(
            field.MasterDataGroupCode);

        var collectionReference = scopeRoot switch
        {
            "EMPLOYEE" => "employee.customFields",
            "EMPLOYMENT" => "employment.customFields",
            "PAYROLL_ELEMENT" => "payrollElement.customFields",
            _ => "masterData.customFields"
        };

        return new PayrollDataReferenceDefinition
        {
            // A csoportkód már azonosítja a mezőcsoportot.
            // Például: KIEGALAPADAT.TAVOL
            Reference =
                $"{field.MasterDataGroupCode}.{field.FieldCode}",
            DisplayName =
                $"{field.MasterDataGroupName} — {field.DisplayName}",
            DataType = GetDynamicLogicalType(field.FieldTypeCode),
            Category = field.MasterDataGroupName,
            Scope = scopeRoot == "MASTER_DATA"
                ? "Törzsadat"
                : scopeRoot,
            CollectionReference = collectionReference,
            ValueCatalog = null,
            Description =
                $"Dinamikus mező. Kód: {field.FieldCode}; " +
                $"mezőtípus: {field.FieldTypeName} ({field.FieldTypeCode}); " +
                $"kötelező: {field.IsRequired}; egyedi: {field.IsUnique}."
        };
    }

    private static PayrollDataReferenceDefinition CreateCollectionReference(
        string collectionReference,
        PayrollDataReferenceDefinition source)
    {
        var displayName = GetCollectionLabel(collectionReference);

        return new PayrollDataReferenceDefinition
        {
            Reference = collectionReference,
            DisplayName = displayName,
            DataType = "collection",
            Category = source.Category,
            Scope = source.Scope,
            CollectionReference = null,
            ValueCatalog = null,
            Description =
                $"Gyűjteményhivatkozás: {collectionReference}. " +
                "A gyűjtemény elemei forEach művelettel dolgozhatók fel."
        };
    }

    private static string GetCollectionLabel(string collectionReference)
        => collectionReference switch
        {
            "PAYROLL_RUN.PreviousResults" =>
                "Korábbi számfejtési eredmények",
            "employee.employments" => "Munkaviszonyok",
            "employee.employments.workSchedules" =>
                "Munkarend-hozzárendelések",
            "workSchedule.days" => "Munkarend ciklusnapjai",
            "employee.absences" => "Távollétek",
            "employee.employments.payrollElements" =>
                "Munkaviszony bérelemei",
            "employee.bankAccounts" => "Bankszámlák",
            "employee.dependents" => "Munkavállalói eltartottak",
            "employee.previousPayrollResults" =>
                "Korábbi számfejtési eredmények",
            "payrollRun.constants" => "Számfejtési konstansok",
            "employee.customFields" => "Munkavállalói egyedi mezők",
            "employment.customFields" => "Munkaviszony egyedi mezők",
            "payrollElement.customFields" => "Bérelem egyedi mezők",
            "masterData.customFields" => "Törzsadat egyedi mezők",
            _ => Humanize(collectionReference)
        };

    private static string GetDynamicScopeRoot(string groupCode)
    {
        if (groupCode.StartsWith(
                "EMPLOYEE",
                StringComparison.OrdinalIgnoreCase))
        {
            return "EMPLOYEE";
        }

        if (groupCode.StartsWith(
                "EMPLOYMENT",
                StringComparison.OrdinalIgnoreCase))
        {
            return "EMPLOYMENT";
        }

        if (groupCode.StartsWith(
                "PAYROLLELEMENT_",
                StringComparison.OrdinalIgnoreCase))
        {
            return "PAYROLL_ELEMENT";
        }

        return "MASTER_DATA";
    }

    private static string GetDynamicLogicalType(string fieldTypeCode)
        => fieldTypeCode.ToUpperInvariant() switch
        {
            "NUMBER" => "decimal",
            "DATE" => "date",
            "BOOLEAN" => "boolean",
            "TEXT" or "LIST" => "text",
            _ => "text"
        };

    private static string GetSqlLogicalType(string sqlType)
        => sqlType.ToLowerInvariant() switch
        {
            "bit" => "boolean",
            "date" => "date",
            "time" => "time",
            "datetime" or "datetime2" or "smalldatetime" => "datetime",
            "decimal" or "numeric" or "money" or "smallmoney"
                or "float" or "real" => "decimal",
            "tinyint" or "smallint" or "int" or "bigint" => "integer",
            "uniqueidentifier" => "guid",
            _ => "text"
        };

    private static bool Matches(
        PayrollDataReferenceDefinition reference,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Contains(reference.Reference, search) ||
               Contains(reference.DisplayName, search) ||
               Contains(reference.Category, search) ||
               Contains(reference.Scope, search) ||
               Contains(reference.Description, search);
    }

    private static bool Contains(string? value, string search)
        => value?.Contains(
            search,
            StringComparison.OrdinalIgnoreCase) == true;

    private static bool Same(string left, string right)
        => string.Equals(
            left,
            right,
            StringComparison.OrdinalIgnoreCase);

    private static string GetTableLabel(string tableName)
        => tableName switch
        {
            "Employee" => "Munkavállaló",
            "Employment" => "Munkaviszony",
            "EmploymentWorkSchedule" => "Munkarend-hozzárendelés",
            "WorkSchedule" => "Munkarend",
            "WorkScheduleDay" => "Munkarend ciklusnapja",
            "DayType" => "Naptípus",
            "EmployeeAbsence" => "Munkavállalói távollét",
            "AbsenceType" => "Távolléttípus",
            "EmploymentPayrollElement" => "Munkaviszony béreleme",
            "PayrollElement" => "Bérelem",
            "EmployeeBankAccount" => "Munkavállalói bankszámla",
            "EmployeeDependent" => "Munkavállalói eltartott",
            "Dependent" => "Eltartott",
            "Company" => "Cég",
            "Constant" => "Számfejtési konstans",
            "PayrollResult" => "Korábbi számfejtési eredmény",
            "RelationshipType" => "Kapcsolat típusa",
            _ => Humanize(tableName)
        };

    private static string GetColumnLabel(string columnName)
        => columnName switch
        {
            "EmployeeId" => "Munkavállaló-azonosító",
            "EmploymentId" => "Munkaviszony-azonosító",
            "WorkScheduleId" => "Munkarend-azonosító",
            "DayTypeId" => "Naptípus-azonosító",
            "AbsenceTypeId" => "Távolléttípus-azonosító",
            "PayrollElementId" => "Bérelem-azonosító",
            "CompanyId" => "Cégazonosító",
            "ConstantId" => "Konstanskód",
            "ConstantName" => "Konstans neve",
            "Value" => "Érték",
            "ValidFrom" => "Érvényesség kezdete",
            "ValidTo" => "Érvényesség vége",
            "CycleStartDate" => "Munkarendciklus kezdete",
            "PeriodLengthDays" => "Ciklus hossza napokban",
            "IsActive" => "Aktív",
            "IsWorkday" => "Munkanap",
            "StartDate" => "Kezdődátum",
            "EndDate" => "Záródátum",
            "Amount" => "Összeg",
            "BirthDate" => "Születési dátum",
            "JoinDate" => "Belépés dátuma",
            "PayrollRunFrom" => "Számfejtés kezdődátuma",
            _ => Humanize(columnName)
        };

    private static string GetScope(string tableName)
        => tableName switch
        {
            "Employee" => "Munkavállaló",
            "Employment" or "Company" => "Munkaviszony",
            "EmploymentWorkSchedule" or "WorkSchedule" or
                "WorkScheduleDay" or "DayType" => "Munkarend",
            "EmployeeAbsence" or "AbsenceType" => "Távollét",
            "EmploymentPayrollElement" or "PayrollElement" => "Bérelem",
            "EmployeeBankAccount" => "Bankszámla",
            "EmployeeDependent" or "Dependent" or "RelationshipType"
                => "Eltartott",
            "Constant" => "Számfejtési konstans",
            "PayrollResult" => "Korábbi számfejtési eredmény",
            _ => "Adatbázisadat"
        };

    private static string? GetCollectionReference(string tableName)
        => tableName switch
        {
            "Employment" => "employee.employments",
            "EmploymentWorkSchedule" =>
                "employee.employments.workSchedules",
            "WorkScheduleDay" => "workSchedule.days",
            "EmployeeAbsence" => "employee.absences",
            "EmploymentPayrollElement" =>
                "employee.employments.payrollElements",
            "EmployeeBankAccount" => "employee.bankAccounts",
            "EmployeeDependent" => "employee.dependents",
            "PayrollResult" => "employee.previousPayrollResults",
            "Constant" => "payrollRun.constants",
            _ => null
        };

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var result = new System.Text.StringBuilder(value.Length + 8);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (index > 0 &&
                char.IsUpper(character) &&
                !char.IsUpper(value[index - 1]))
            {
                result.Append(' ');
            }

            result.Append(character);
        }

        return result.ToString();
    }
}