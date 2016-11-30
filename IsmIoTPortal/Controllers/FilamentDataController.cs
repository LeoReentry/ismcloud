using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using IsmIoTPortal.Models;

namespace IsmIoTPortal.Controllers
{
    public class FilamentDataController : Controller
    {
        private IsmIoTPortalContext db = new IsmIoTPortalContext();

        // GET: FilamentData
        public ActionResult Index()
        {
            var filamentData = db.FilamentData.Include(f => f.IsmDevice);
            return View(filamentData.ToList());
        }

        // GET: FilamentData/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FilamentData filamentData = db.FilamentData.Find(id);
            if (filamentData == null)
            {
                return HttpNotFound();
            }
            return View(filamentData);
        }

        // GET: FilamentData/Create
        public ActionResult Create()
        {
            ViewBag.IsmDeviceId = new SelectList(db.IsmDevices, "IsmDeviceId", "DeviceId");
            return View();
        }

        // POST: FilamentData/Create
        // Aktivieren Sie zum Schutz vor übermäßigem Senden von Angriffen die spezifischen Eigenschaften, mit denen eine Bindung erfolgen soll. Weitere Informationen 
        // finden Sie unter http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "FilamentDataId,Time,FC,FL,H1,H2,H3,H4,H5,H6,H7,H8,H9,H10,IsmDeviceId,DeviceId,BlobUriImg,BlobUriColoredImg")] FilamentData filamentData)
        {
            if (ModelState.IsValid)
            {
                db.FilamentData.Add(filamentData);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.IsmDeviceId = new SelectList(db.IsmDevices, "IsmDeviceId", "DeviceId", filamentData.IsmDeviceId);
            return View(filamentData);
        }

        // GET: FilamentData/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FilamentData filamentData = db.FilamentData.Find(id);
            if (filamentData == null)
            {
                return HttpNotFound();
            }
            ViewBag.IsmDeviceId = new SelectList(db.IsmDevices, "IsmDeviceId", "DeviceId", filamentData.IsmDeviceId);
            return View(filamentData);
        }

        // POST: FilamentData/Edit/5
        // Aktivieren Sie zum Schutz vor übermäßigem Senden von Angriffen die spezifischen Eigenschaften, mit denen eine Bindung erfolgen soll. Weitere Informationen 
        // finden Sie unter http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "FilamentDataId,Time,FC,FL,H1,H2,H3,H4,H5,H6,H7,H8,H9,H10,IsmDeviceId,DeviceId,BlobUriImg,BlobUriColoredImg")] FilamentData filamentData)
        {
            if (ModelState.IsValid)
            {
                db.Entry(filamentData).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.IsmDeviceId = new SelectList(db.IsmDevices, "IsmDeviceId", "DeviceId", filamentData.IsmDeviceId);
            return View(filamentData);
        }

        // GET: FilamentData/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FilamentData filamentData = db.FilamentData.Find(id);
            if (filamentData == null)
            {
                return HttpNotFound();
            }
            return View(filamentData);
        }

        // POST: FilamentData/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            FilamentData filamentData = db.FilamentData.Find(id);
            db.FilamentData.Remove(filamentData);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
