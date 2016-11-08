using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using IsmIoTPortal.Models;

namespace IsmIoTPortal.Controllers
{
    /// <summary>
    /// Controller to manage devices with pending approval.
    /// </summary>
    public class ApproveDevicesController : Controller
    {
        private readonly IsmIoTPortalContext db = new IsmIoTPortalContext();
        // GET: ApproveDevices
        public ActionResult Index()
        {
            var ismDevices = db.NewDevices.Include(i => i.Hardware).Include(i => i.Location).Include(i => i.Software);
            return View(ismDevices.ToList());
        }
    }
}