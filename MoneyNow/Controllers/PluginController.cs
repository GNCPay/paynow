using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MoneyNow.Controllers
{
    public class PluginController:Controller
    {
        public ActionResult Send()
        {
            return View();
        }

        public ActionResult Receive()
        {
            return View();
        }

        public ActionResult Sent()
        {
            return View();
        }

        public ActionResult Topup()
        {
            return View();
        }
    }
}