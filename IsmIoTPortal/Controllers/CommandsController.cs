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
    [IsmAuthorize(Groups = "DeviceAdmins, Admins")]
    public class CommandsController : Controller
    {
        private IsmIoTPortalContext db = new IsmIoTPortalContext();

        // GET: Commands
        public ActionResult Index()
        {
            var commands = db.Commands.Include(c => c.IsmDevice);
            return View(commands.ToList());
        }

        // GET: Commands/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Command command = db.Commands.Find(id);
            if (command == null)
            {
                return HttpNotFound();
            }
            return View(command);
        }

        // GET: Commands/Create
        public ActionResult Create()
        {
            ViewBag.IsmDeviceId = new SelectList(db.IsmDevices, "IsmDeviceId", "DeviceId");
            return View();
        }

        // POST: Commands/Create
        // Aktivieren Sie zum Schutz vor übermäßigem Senden von Angriffen die spezifischen Eigenschaften, mit denen eine Bindung erfolgen soll. Weitere Informationen 
        // finden Sie unter http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "CommandId,Cmd,Timestamp,CommandStatus,IsmDeviceId")] Command command)
        {
            if (ModelState.IsValid)
            {
                db.Commands.Add(command);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.IsmDeviceId = new SelectList(db.IsmDevices, "IsmDeviceId", "DeviceId", command.IsmDeviceId);
            return View(command);
        }

        // GET: Commands/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Command command = db.Commands.Find(id);
            if (command == null)
            {
                return HttpNotFound();
            }
            ViewBag.IsmDeviceId = new SelectList(db.IsmDevices, "IsmDeviceId", "DeviceId", command.IsmDeviceId);
            return View(command);
        }

        // POST: Commands/Edit/5
        // Aktivieren Sie zum Schutz vor übermäßigem Senden von Angriffen die spezifischen Eigenschaften, mit denen eine Bindung erfolgen soll. Weitere Informationen 
        // finden Sie unter http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "CommandId,Cmd,Timestamp,CommandStatus,IsmDeviceId")] Command command)
        {
            if (ModelState.IsValid)
            {
                db.Entry(command).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.IsmDeviceId = new SelectList(db.IsmDevices, "IsmDeviceId", "DeviceId", command.IsmDeviceId);
            return View(command);
        }

        // GET: Commands/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Command command = db.Commands.Find(id);
            if (command == null)
            {
                return HttpNotFound();
            }
            return View(command);
        }

        // POST: Commands/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Command command = db.Commands.Find(id);
            db.Commands.Remove(command);
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
