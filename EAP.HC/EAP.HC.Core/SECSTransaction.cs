using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace EAP.HC.Core
{

    [XmlRoot(ElementName = "SECSLibrary")]
    public class SECSLibrary
    {
        public List<SECSTransaction> SECSTransactions { get; set; }
    }

    public class SECSTransaction
    {
        public string MessageName;

        public string Description;

        public int Stream;

        public int Function;

        public string Direction;

        public bool Wait;

        public bool AutoReply;



        [XmlIgnore] // 忽略 DataItem 的复杂结构
        public string DataItemContent { get; set; }

        // 用于反序列化时存储 DataItem 的内容
        [XmlElement("DataItem")]
        public string DataItem
        {
            get { return DataItemContent; }
            set { DataItemContent = value; }
        }

        [XmlIgnore]
        public SecsMessage message;
    }


}
