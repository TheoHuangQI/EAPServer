using EAP.HC.Core;
using EAP.HC.Core.Entities.MES;
using EAP.RC.MES;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EAP.EC.KS_CM300
{
    public class KS_CM300
    {

        public void ReciveMsGS1F1(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            var rootItem = primaryMessage.PrimaryMessage.SecsItem;
        }

        public void ReciveMsGS5F1(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            var rootItem = primaryMessage.PrimaryMessage.SecsItem;
        }




        public void ReciveMsGS6F11(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            var rootItem = primaryMessage.PrimaryMessage.SecsItem;
            Item replyItem = Item.B((byte)0);
            SecsMessage replyMsg = new SecsMessage(6, 12);
            replyMsg.SecsItem = replyItem;
            primaryMessage.TryReplyAsync(replyMsg);

            var dataid = int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[0]).ToString());
            var ceid =int.Parse(SecsItemUtil.GetItemValue(rootItem.Items[1]).ToString());
            switch (ceid)
            {
                case 37:
                    WaitingForHost(context.EquipmentID,rootItem);
                    break;
                case 48:

                    break;
            }
        }

             
        #region 
        public void WaitingForHost(string EqpID,Item rootItem)
        {
            if (rootItem.Items.Length != 3)
            {
                return;
            }
            var portstr = SecsItemUtil.GetItemValue(rootItem.Items[2][1][1][0]).ToString().Trim();
            var carrierId = SecsItemUtil.GetItemValue(rootItem.Items[2][2][1][0]).ToString().Trim();
            var portId = -1;
            switch (portstr)
            {
                case "LP1":
                    portId = 1;
                    break;
                case "LP2":
                    portId = 2;
                    break;
                case "LP3":
                    portId = 3;
                    break;
                case "LP4":
                    portId = 4;
                    break;
            }
           var mesbatchInfo= MESService.MES_LotInfoDownloadRequest(carrierId);
            if (mesbatchInfo == null)
                return;
            if (!AppCache.Current.MesBatchInfos.ContainsKey(mesbatchInfo.BATCH_ID))
            {
                AppCache.Current.MesBatchInfos.Add(mesbatchInfo.BATCH_ID, mesbatchInfo);
            }
            Send_FirstProcessWithCarrier(EqpID, portId, carrierId);
        }

        public void Send_FirstProcessWithCarrier(string EqpId,int portId,string carrierId)
        {
            SecsMessage msg = new SecsMessage(3, 17);
            Item[] items = new Item[] { Item.U4((byte)0), Item.A("ProceedWithCarrier"), Item.A(carrierId),Item.U1((byte)portId),Item.L()};
            msg.SecsItem = Item.L(items);
            var context = EAPContext.contexts.First(a => a.EquipmentID == EqpId);
            var reply =context.SecsGem.SendAsync(msg);
        }



        #endregion Handler
        public void SecsMsgHandler(ContextObject context, PrimaryMessageWrapper primaryMessage)
        {
            try
            {
                Console.Write(primaryMessage.ToString());
                var messageId = primaryMessage.Id;
                var stream = primaryMessage.PrimaryMessage.S;
                var function = primaryMessage.PrimaryMessage.F;
                switch (stream)
                {
                    case 1:
                        switch (function)
                        {
                            case 1:
                                ReciveMsGS1F1(context, primaryMessage);
                                break;
                        }
                        break;
                    case 5:
                        switch(function)
                        {
                            case 1:
                                ReciveMsGS5F1(context, primaryMessage);
                                break;
                        }
                        break;
                    case 6:
                        switch (function)
                        {
                            case 11:
                                ReciveMsGS6F11(context, primaryMessage);
                                break;
                        }
                        break;
                    default:
                        break;
                }
            }
            catch(Exception ex)
            {
                Console.Write(ex.Message);
            }
        }
    }
}
