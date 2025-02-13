using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EAP.HC.Core
{
    public class ContextObject
    {
        private string equipmentId;

        private string eqpType;

        private SecsGem secsgem;

        public string EquipmentID
        {
            get
            {
                return equipmentId;
            }
            set
            {
                equipmentId = value;
            }
        }

        public string EqpType
        {
            get
            {
                return eqpType;
            }
            set
            {
                eqpType = value;
            }
        }

        public SecsGem SecsGem
        {
            get
            {
                return secsgem;
            }
            set
            {
                secsgem = value;
            }
        }
    }
}
