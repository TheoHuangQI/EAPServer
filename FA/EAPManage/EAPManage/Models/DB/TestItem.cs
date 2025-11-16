using SqlSugar;

namespace EAPManage.Models.DB
{
    [SugarTable("TEST_ITEM")]
    public class TestItem
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "MESSAGE_NAME", ColumnDataType = "TEXT")]
        public string MessageName { get; set; }

        [SugarColumn(ColumnName = "DESCRIPTION", ColumnDataType = "TEXT")]
        public string Description { get; set; }

        [SugarColumn(ColumnName = "STREAM", ColumnDataType = "INTEGER")]
        public int Stream { get; set; }

        [SugarColumn(ColumnName = "FUNCTION", ColumnDataType = "INTEGER")]
        public int Function { get; set; }

        [SugarColumn(ColumnName = "DIRECTION", ColumnDataType = "TEXT")]
        public string Direction { get; set; }

        [SugarColumn(ColumnName = "WAIT", ColumnDataType = "INTEGER")]
        public int Wait { get; set; }

        [SugarColumn(ColumnName = "AUTO_REPLY", ColumnDataType = "INTEGER")]
        public int AutoReply { get; set; }

        [SugarColumn(ColumnName = "DATA_ITEM_CONTENT", ColumnDataType = "TEXT")]
        public string DataItemContent { get; set; }

        [SugarColumn(ColumnName = "REPLY_STREAM", ColumnDataType = "INTEGER")]
        public int ReplyStream { get; set; }

        [SugarColumn(ColumnName = "REPLY_FUNCTION", ColumnDataType = "INTEGER")]
        public int ReplyFunction { get; set; }

        [SugarColumn(ColumnName = "REPLY_DATA_ITEM_CONTENT", ColumnDataType = "TEXT")]
        public string ReplyDataItemContent { get; set; }

    }
}
