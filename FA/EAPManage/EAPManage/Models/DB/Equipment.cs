using SqlSugar;

namespace EAPManage.Models.DB
{

    [SugarTable("Equipment")]
    public class Equipment
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "EQPID", ColumnDataType = "TEXT")]
        public string EqpId { get; set; }

        [SugarColumn(ColumnName = "EQP_TYPE", ColumnDataType = "TEXT")]
        public string EqpType { get; set; }

        [SugarColumn(ColumnName = "ENABLE", ColumnDataType = "INTEGER")]
        public int Enable { get; set; }

        [SugarColumn(ColumnName = "EQP_STATUS", ColumnDataType = "INTEGER")]
        public int EqpStatus { get; set; }

        [SugarColumn(ColumnName = "EAP_STATUS", ColumnDataType = "INTEGER")]
        public int EapStatus { get; set; }

        [SugarColumn(ColumnName = "CONTROL_STATE", ColumnDataType = "INTEGER")]
        public int ControlState { get; set; }

        [SugarColumn(IsIgnore = true)]
        public List<PortInfo> portInfos { get; set; }
    }
}
