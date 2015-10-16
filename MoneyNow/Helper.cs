using MongoDB.Driver.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MoneyNow
{
    public class Helper
    {
        public static eWallet.Data.MongoHelper Data = null;
        public static dynamic banknet_config = null;
        public static void Init()
        {
            Data = new eWallet.Data.MongoHelper("mongodb://127.0.0.1:27017/moneynow", "moneynow");
            banknet_config = Data.Get("config", Query.EQ("_id", "partner_bank_banknet"));
        }

        private static banknet.PaymentGatewayPortTypeClient client = null;
        public static banknet.PaymentGatewayPortTypeClient BanknetClient
        {
            get
            {
                if(client == null || client.State!= System.ServiceModel.CommunicationState.Opened)
                {
                    client = new banknet.PaymentGatewayPortTypeClient();
                    client.Open();
                }
                return client;
            }
        }
    }
}