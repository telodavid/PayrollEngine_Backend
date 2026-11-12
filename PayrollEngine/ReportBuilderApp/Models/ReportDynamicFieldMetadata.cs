namespace PayrollEngine.ReportBuilderApp.Models;

public sealed class ReportDynamicFieldMetadata
{
    public int MasterDataFieldId { get; set; }

    public string FieldCode { get; set; } = "";
    public string DisplayName { get; set; } = "";

    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; }
    public bool IsUnique { get; set; }

    public int MasterDataGroupId { get; set; }

    public string GroupCode { get; set; } = "";
    public string GroupName { get; set; } = "";

    public string FieldTypeCode { get; set; } = "";
    public string FieldTypeName { get; set; } = "";

    public string OwnerType { get; set; } = "";
    public string OwnerSchema { get; set; } = "";
    public string OwnerEntity { get; set; } = "";
    public string OwnerIdColumn { get; set; } = "";

    public string ValueSchema { get; set; } = "";
    public string ValueEntity { get; set; } = "";

    public string OwnerForeignKeyColumn { get; set; } = "";
    public string FieldIdColumn { get; set; } = "";
    public string ValueColumn { get; set; } = "";
    public string OptionIdColumn { get; set; } = "";

    public string? ValidFromColumn { get; set; }
    public string? ValidToColumn { get; set; }
}