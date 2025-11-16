using EAPManage.Models.DB;
using EAPManage.Models.Enum;
using SqlSugar;
using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [Serializable]
    [XmlRoot("Body")]
    public class EAPTestItemStart:MQBody
    {
        public string TestCaseName { get; set; }
        public EAPTestItem EAPTestItem { get; set; }
    }

    [Serializable]
    [XmlRoot("Body")]
    public class EAPTestBatchStart : MQBody
    {
        public string TestCaseName { get; set; }
        public EAPTestBatch EAPTestBatch { get; set; }
    }


    public class EAPTestCase
    {
        public string eqpId { get; set; }
        public string TestCaseName { get; set; }
        
        public EAPTestTemplate EAPTestTemplate { get; set; }
    }


    public class EAPTestTemplate
    {
        public string TestTempName { get; set; }
        public List<EAPTestItem> TestItems { get; set; }

        public List<EAPTestBatch> TestBatches { get; set; }
    }


    public class EAPTestItem
    {
        public int SerialNumber { get; set; }
        public string MessageName { get; set; }

        public string Description { get; set; }

        public int Stream { get; set; }

        public int Function { get; set; }

        public string Direction { get; set; }

        public int Wait { get; set; }

        public int AutoReply { get; set; }

        public string DataItemContent { get; set; }

        public int ReplyStream { get; set; }

        public int ReplyFunction { get; set; }

        public string ReplyDataItemContent { get; set; }
    }


    public class EAPTestBatch
    {
        public string TestBatchName { get; set; }
        public int SerialNumber { get; set; }
        public TestType TestType { get; set; }
        public string StartEvent { get; set; }
        public string EndEvent { get; set; }
        public List<EAPTestItem> TestItems { get; set; }

    }
}
