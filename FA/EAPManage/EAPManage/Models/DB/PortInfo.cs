using SqlSugar;

namespace EAPManage.Models.DB
{
    [SugarTable("PORTINFO")]
    public class PortInfo
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "PORT_ID", ColumnDataType = "INTEGER")]
        public int portId { get; set; }

        [SugarColumn(ColumnName = "EQPID", ColumnDataType = "TEXT")]
        public string eqpId { get; set; }

        [SugarColumn(ColumnName = "PORT_NAME", ColumnDataType = "TEXT")]
        public string portName { get; set; }

        [SugarColumn(ColumnName = "CARRIERID", ColumnDataType = "TEXT")]
        public string carrierId { get; set; }

        [SugarColumn(ColumnName = "LOTID", ColumnDataType = "TEXT")]
        public string lotId { get; set; }

        [SugarColumn(ColumnName = "SLOT_MAP", ColumnDataType = "TEXT")]
        public string slotMap { get; set; }

        [SugarColumn(ColumnName = "PJ_SLOT", ColumnDataType = "TEXT")]
        public string pjSlot { get; set; }

        [SugarColumn(ColumnName = "PRO_COMPLETE_SLOT", ColumnDataType = "TEXT")]
        public string proComSlot { get; set; }

        [SugarColumn(ColumnName = "ACESS_MODE", ColumnDataType = "INTEGER")]
        public int accessMode { get; set; }

                [SugarColumn(ColumnName = "TRANSFER_STATE", ColumnDataType = "INTEGER")]
        public int transferState { get; set; }

        [SugarColumn(ColumnName = "LOOP_PROCESS_STATE", ColumnDataType = "INTEGER", DefaultValue = "0")]
        public int loopProcessState { get; set; } = 0;

        // 0 = 原进原出，1 = Transfer。Transfer LP 还通过角色和配对 LP 标识出片/进片关系。
        [SugarColumn(ColumnName = "JOB_MODE", ColumnDataType = "INTEGER", DefaultValue = "0")]
        public int jobMode { get; set; } = 0;

        // 0 = None，1 = 出片 LP，2 = 进片 LP。
        [SugarColumn(ColumnName = "TRANSFER_ROLE", ColumnDataType = "INTEGER", DefaultValue = "0")]
        public int transferRole { get; set; } = 0;

        [SugarColumn(ColumnName = "TRANSFER_PARTNER_PORT_ID", ColumnDataType = "INTEGER", DefaultValue = "0")]
        public int transferPartnerPortId { get; set; } = 0;
    }
}
