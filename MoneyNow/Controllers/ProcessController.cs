using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MoneyNow.Controllers
{
    public class ProcessController : Controller
    {
        // GET: Process
        public ActionResult Index()
        {
            return View();
        }

        public void LoginUser(string user_id, string name, string email, string picture, string access_token)
        {
            if (Session["user_id"] == null)
            {
                dynamic profile = Helper.Data.Get("profile", Query.EQ("_id", user_id));
                Session["user_id"] = user_id;
                Session["access_token"] = access_token;
                if (profile == null)
                {
                    profile = new eWallet.Data.DynamicObj();
                    profile._id = user_id;
                    profile.email = email;
                    profile.name = name;
                    profile.picture = picture;
                    Helper.Data.Insert("profile", profile);
                }
            }
            ///Logic
            /// Check xem user ton tai ko
            /// - Neu ko, dang ky
            /// - Neu roi, by pass
            /// 
        }

        public JsonResult Send(string receiver, long amount, string message, string pass_code, string bank)
        {
            string trans_id = Helper.Data.GetNextSquence("trans_" + DateTime.Today.ToString("YYMMDD")).ToString().PadLeft(6, '0');
            JArray array = JArray.Parse(receiver);
            List<eWallet.Data.DynamicObj> list_receiver = new List<eWallet.Data.DynamicObj>();
            foreach (dynamic arr in array)
                list_receiver.Add(new eWallet.Data.DynamicObj(arr.ToString()));

            dynamic response = new eWallet.Data.DynamicObj();
            dynamic tran_info = new eWallet.Data.DynamicObj();

            tran_info._id = Guid.NewGuid().ToString();
            tran_info.trans_id = trans_id;
            tran_info.type = "SEND_MONEY";
            tran_info.profile = Session["user_id"];
            tran_info.per_amount = amount;
            tran_info.total_amount = amount * list_receiver.Count;
            tran_info.message = message;
            //dynamic list = JObject.Parse(receiver);
            tran_info.receiver = list_receiver.ToArray();
            //foreach(var item in array)
            //{
            //    tran_info.receiver.pus
            //}
            tran_info.is_split = false;

            tran_info.pass_code = pass_code;
            tran_info.is_split_with_me = false;
            tran_info.source_account = new eWallet.Data.DynamicObj();
            tran_info.source_account.type = "ATM";
            tran_info.source_account.bank_code = bank;
            string url_confirm = "transaction_type=cashtosend&trans_id=" + tran_info._id + "&amount=" + tran_info.total_amount;

            string bank_net_result = SendOrder(
                "CASHTOSEND",
                trans_id,
                "moneynow",
                tran_info.total_amount.ToString(),
                "0",
                "0",
                bank,
                url_confirm
                );
            tran_info.partner_return = bank_net_result;

            string[] url_params = bank_net_result.Split('|');
            if (url_params[0] == "010")
            {
                url_params[2] = url_params[2].Substring(0, int.Parse(url_params[1]));
                response.url_redirect = url_params[2].Substring(0, int.Parse(url_params[1]));
                response.error_code = "00";
                response.error_message = "Khoi tao giao dich thanh cong";
                tran_info.status = "CASHIN";
                tran_info.partner_trans_id = response.url_redirect.Split('=')[1];
                
                Helper.Data.Insert("transactions", tran_info);

                return Json(new { error_code = response.error_code, error_message = response.error_message, url_redirect = response.url_redirect }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(new { error_code = "96", error_message = "Có lỗi trong quá trình xử lý. Vui lòng thử lại sau", url_redirect = "" }, JsonRequestBehavior.AllowGet);
            }
        }
        public string SendOrder(string service_code, string trans_id, string product_code, string amount,
            string shipping_fee, string tax, string bank_code, string confirm_url)
        {
            string trans_key = String.Concat(trans_id, Helper.banknet_config.merchant_code, product_code, amount, shipping_fee, tax, Helper.banknet_config.merchant_key);
            trans_key = eWallet.Common.Security.GenMd5Hash(trans_key);
            return Helper.BanknetClient.Send_GoodInfo_Ext2(trans_id, Helper.banknet_config.merchant_code, Helper.banknet_config.country_code, product_code, string.Empty,
                amount, shipping_fee, tax, Helper.banknet_config.url_success + "?" + confirm_url, Helper.banknet_config.url_fail + "?" + confirm_url, trans_key, bank_code, "720");
        }

        public string ConfirmOrder(string trans_id, string partner_trans_id)
        {
            string trans_key = String.Concat(trans_id, partner_trans_id, Helper.banknet_config.merchant_code, Helper.banknet_config.merchant_key);
            trans_key = eWallet.Common.Security.GenMd5Hash(trans_key);
            return Helper.BanknetClient.ConfirmTransactionResult(trans_id, partner_trans_id, Helper.banknet_config.merchant_code, "0", trans_key);
        }

        public ActionResult Success(string transaction_type, string trans_id, string amount)
        {
            dynamic trans_info = Helper.Data.Get("transactions", Query.EQ("_id", trans_id));
            if (trans_info.status != "CASHIN")
            {
                ViewBag.Result = "đã được xử lý. Vui lòng liên hệ với chúng tôi để được hỗ trợ";
                return View("Result");
            }
            string confirmBankNet = ConfirmOrder(trans_info.trans_id, trans_info.partner_trans_id);
            trans_info.partner_confirm_return = confirmBankNet;
            trans_info.status = "PROCESSING";
            Helper.Data.Save("transactions", trans_info);

            var fb = new Facebook.FacebookClient(Session["access_token"].ToString());
            foreach (dynamic receiver in trans_info.receiver)
            {
                dynamic result = fb.Post("me/feed", new
                {
                    message = trans_info.message + ". Click để nhận tiền",
                    link = "http://moneynow.me/receiver/" + trans_info._id,
                    caption = "Gửi tới bạn "                     + trans_info.per_amount.ToString("N0"),
                    tags = receiver.id,
                    privacy= new {value="SELF"}
                });
            }
            //"/me/feed",
            //            "POST",
            //            {
            //    "message": message + '. Click để nhận tiền',
            //                "link": 'http://moneynow.me/receive/' + code,
            //                "caption": userFullName + ' gửi tới bạn ' + amount + 'đ',
            //                "tags": selectedFriends,
            //                "privacy": { 'value': 'SELF' }
            //},
            //var newPostId = result.id;
            //client.Get("/me")
            ViewBag.Result = "thành công";
            return View("Result");
        }
        public ActionResult Fail(string transaction_type, string trans_id, string amount)
        {
            dynamic trans_info = Helper.Data.Get("transactions", Query.EQ("_id", trans_id));
            //if (trans_info.status != "CASHIN")
            //{
            //    ViewBag.Result = "đã được xử lý. Vui lòng liên hệ với chúng tôi để được hỗ trợ";
            //    return View("Result");
            //}
            //string confirmBankNet = ConfirmOrder(trans_info.trans_id, trans_info.partner_trans_id);
            trans_info.status = "ERROR";
            Helper.Data.Save("transactions", trans_info);
            ViewBag.Result = "không thành công.  Vui lòng liên hệ với chúng tôi để được hỗ trợ";
            return View("Result");
        }

        public ActionResult Result()
        {
            return View();
        }
    }
}