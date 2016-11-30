using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace IsmIoTPortal.Controllers
{
    /// <summary>
    /// This controller serves custom error pages to the client.
    /// </summary>
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        // GET: Index
        public ActionResult Index()
        {
            return View();
        }

        // GET: NotFound
        public ActionResult NotFound()
        {
            return View();
        }

        // GET: BadRequest
        public ActionResult BadRequest()
        {
            return View();
        }

        // GET: Unavailable
        public ActionResult Unavailable()
        {
            return View();
        }
    }
}