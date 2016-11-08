using System.Data.Entity;
using System.Linq;
using System.Net;
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

        public ActionResult Approve(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            NewDevice device = db.NewDevices.Find(id);
            if (device == null)
            {
                return HttpNotFound();
            }
            device.Approved = true;
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        public ActionResult Remove(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            NewDevice device = db.NewDevices.Find(id);
            if (device == null)
            {
                return HttpNotFound();
            }
            db.NewDevices.Remove(device);
            db.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}