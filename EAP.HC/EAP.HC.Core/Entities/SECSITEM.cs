using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core.Entities
{
    public class SECSITEM
    {
        public string Type;
        public int count;
        public bool Fixed;
        public string ItemName;
        public string Value;
        public List<SECSITEM> ChildItems;
    }
}
