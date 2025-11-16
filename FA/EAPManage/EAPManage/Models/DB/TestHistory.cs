using EAPManage.Models.Enum;
using SqlSugar;

namespace EAPManage.Models.DB
{

    [SugarTable("TEST_HISTORY")]
    public class TestHistory
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "TEST_CASE_ID", ColumnDataType = "TEXT")]
        public string TestCaseId { get; set; }

        [SugarColumn(ColumnName = "TEST_CASE_NAME", ColumnDataType = "TEXT")]
        public string TestCaseName { get; set; }

        [SugarColumn(ColumnName = "TEST_TEMPLATE_NAME", ColumnDataType = "TEXT")]
        public string TestTemplateName { get; set; }

        [SugarColumn(ColumnName = "SERIAL_NUMBER", ColumnDataType = "INTEGER")]
        public int SerialNumber { get; set; }

        [SugarColumn(ColumnName = "START_TIME", ColumnDataType = "TEXT")]
        public string StartTime { get; set; }

        [SugarColumn(ColumnName = "END_TIME", ColumnDataType = "TEXT")]
        public string EndTime { get; set; }

        [SugarColumn(ColumnName = "TEST_ITEM_NAME", ColumnDataType = "TEXT")]
        public string TestItemName { get; set; }

        [SugarColumn(ColumnName = "TEST_ITEM_DESC", ColumnDataType = "TEXT")]
        public string TestItemDesc { get; set; }

        [SugarColumn(ColumnName = "TEST_RESULT", ColumnDataType = "INTEGER")]
        public TestResult TestResult { get; set; }
    }
}
