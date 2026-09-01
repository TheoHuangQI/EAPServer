using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace EAPManage.Models.MQ
{
    [Serializable]
    [XmlRoot("Body")]
    public class FdcSvidCollection : MQBody
    {
        public List<int> RequestedSvids { get; set; } = new List<int>();

        public bool SaveToHistory { get; set; } = true;

        public int MaxCount { get; set; }

        public int ChunkSize { get; set; } = 120;

        public string Return { get; set; }

        public string Message { get; set; }

        public string CollectedAt { get; set; }

        public List<FdcSvidValue> Values { get; set; } = new List<FdcSvidValue>();
    }

    [Serializable]
    public class FdcSvidValue
    {
        public int Svid { get; set; }

        public string Name { get; set; }

        public string DefinedType { get; set; }

        public string ActualType { get; set; }

        public string Unit { get; set; }

        public string Value { get; set; }

        public string EcsName { get; set; }

        public string Description { get; set; }

        public string Status { get; set; }

        public string ErrorMessage { get; set; }
    }
}
