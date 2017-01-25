using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace IsmIoTPortal.Controllers
{
    [IsmAuthorize(Groups ="DeviceAdmins")]
    public class DeviceManagementController : Controller
    {
        // GET: DeviceManagement
        public ActionResult Index()
        {
            return View();
        }
    }
}