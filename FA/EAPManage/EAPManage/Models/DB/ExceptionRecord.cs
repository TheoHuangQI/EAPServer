using SqlSugar;

namespace EAPManage.Models.DB
{

    [SugarTable("EXCEPTION_RECORD")]
    public class ExceptionRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "LOTID", ColumnDataType = "TEXT")]
        public string lotId { get; set; }

        [SugarColumn(ColumnName = "Content", ColumnDataType = "TEXT")]
        public string content { get; set; }

        [SugarColumn(ColumnName = "EQPID", ColumnDataType = "TEXT")]
        public string eqpId { get; set; }

        [SugarColumn(ColumnName = "PORTID", ColumnDataType = "INTEGER")]
        public int portId { get; set; }

        [SugarColumn(ColumnName = "PORT_NAME", ColumnDataType = "TEXT")]
        public string portName { get; set; }

        [SugarColumn(ColumnName = "ERROR_TIME", ColumnDataType = "TEXT")]
        public string errorTime { get; set; }

        [SugarColumn(ColumnName = "STATE", ColumnDataType = "INTEGER")]
        public int state { get; set; }

        [SugarColumn(ColumnName = "EQP_TYPE", ColumnDataType = "TEXT")]
        public string eqpType { get; set; }
        
    }
}
