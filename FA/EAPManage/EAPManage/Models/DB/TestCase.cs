using SqlSugar;

namespace EAPManage.Models.DB
{

    [SugarTable("TEST_CASE")]
    public class TestCase
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "EQPID", ColumnDataType = "TEXT")]
        public string EqpId { get; set; }

        [SugarColumn(ColumnName = "TEST_CASE_NAME", ColumnDataType = "TEXT")]
        public string TestCaseName { get; set; }

        [SugarColumn(ColumnName = "TEMP_NAME", ColumnDataType = "TEXT")]
        public string TempName { get; set; }

        [SugarColumn(ColumnName = "TEMP_ID", ColumnDataType = "INTEGER")]
        public int TempId { get; set; }

        [SugarColumn(ColumnName = "STATE", ColumnDataType = "INTEGER")]
        public int State { get; set; }
    }
}
