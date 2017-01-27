using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using IsmIoTPortal.Models;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Azure.KeyVault;
using System.Threading.Tasks;
using System.Configuration;

namespace IsmIoTPortal.Controllers
{
    [IsmAuthorize(Groups ="Admins, DeviceAdmins")]
    public class SoftwareController : Controller
    {
        private IsmIoTPortalContext db = new IsmIoTPortalContext();

        // GET: Software
        public ActionResult Index()
        {
            return View(db.Software.ToList());
        }

        // GET: Software/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Software software = db.Software.Find(id);
            if (software == null)
            {
                return HttpNotFound();
            }
            return View(software);
        }

        // GET: Software/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Software/Create
        // Aktivieren Sie zum Schutz vor übermäßigem Senden von Angriffen die spezifischen Eigenschaften, mit denen eine Bindung erfolgen soll. Weitere Informationen 
        // finden Sie unter http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "SoftwareId,SoftwareVersion,Changelog")] Software software, HttpPostedFileBase upload)
        {
            if (ModelState.IsValid)
            {
                if (upload != null && upload.ContentLength > 0)
                {
                    try
                    {
                        var location = Server.MapPath("~/sw-updates/" + software.SoftwareVersion);
                        // Create path if it doesn't exist
                        Directory.CreateDirectory(location);
                        // Get full path
                        string path = Path.Combine(location, Path.GetFileName(upload.FileName));
                        // Save file
                        upload.SaveAs(path);
                        // Calculate SHA256
                        // Read file
                        var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                        byte[] checksum_b;
                        // Buffered calculation
                        using(var bufferedStream = new BufferedStream(fileStream, 1024 * 32))
                        {
                            var sha = new SHA256Managed();
                            checksum_b = sha.ComputeHash(bufferedStream);
                        }

                        // Get access to key vault to encrypt checksum
                        var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
                        var key = kv.GetKeyAsync(ConfigurationManager.AppSettings["kv:fw-signing-key"]).Result;
                        var publicKey = Convert.ToBase64String(key.Key.N);

                        // Sign checksum
                        var sha256sig = kv.SignAsync(
                            keyIdentifier: ConfigurationManager.AppSettings["kv:fw-signing-key"],
                            algorithm: Microsoft.Azure.KeyVault.WebKey.JsonWebKeySignatureAlgorithm.RS256,
                            digest: checksum_b).Result.Result;
                        // Save public key to disc
                        string keyPath = Path.Combine(location, "key.pub");
                        TextWriter outputStream = new StringWriter();
                        outputStream.WriteLine("-----BEGIN PUBLIC KEY-----");
                        for(Int32 i = 0; i < publicKey.Length; i+= 64)
                        {
                            outputStream.WriteLine(publicKey.ToCharArray(), i, (Int32) Math.Min(64, publicKey.Length - i));
                        }
                        outputStream.WriteLine("-----END PUBLIC KEY-----");
                        System.IO.File.WriteAllText(keyPath, outputStream.ToString());

                        // Save byte data as sig
                        string checksumPath = Path.Combine(location, "sig");
                        System.IO.File.WriteAllBytes(checksumPath, sha256sig);
                        
                        software.Author = "SWT";
                        // Add to database
                        db.Software.Add(software);
                        db.SaveChanges();
                        return RedirectToAction("Index");
                    }
                    catch (Exception e)
                    {

                    }
                }
            }

            return View(software);
        }

        // GET: Software/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Software software = db.Software.Find(id);
            if (software == null)
            {
                return HttpNotFound();
            }
            return View(software);
        }

        // POST: Software/Edit/5
        // Aktivieren Sie zum Schutz vor übermäßigem Senden von Angriffen die spezifischen Eigenschaften, mit denen eine Bindung erfolgen soll. Weitere Informationen 
        // finden Sie unter http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "SoftwareId,SoftwareVersion")] Software software)
        {
            if (ModelState.IsValid)
            {
                db.Entry(software).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(software);
        }

        // GET: Software/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Software software = db.Software.Find(id);
            if (software == null)
            {
                return HttpNotFound();
            }
            return View(software);
        }

        // POST: Software/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Software software = db.Software.Find(id);
            db.Software.Remove(software);
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

        private async Task<string> GetToken(string authority, string resource, string scope)
        {
            var id = ConfigurationManager.AppSettings["hsma-ida:PortalClientId"];
            var secret = ConfigurationManager.AppSettings["hsma-ida:PortalAppKey"];
            return await IsmIoTSettings.IsmUtils.GetAccessToken(authority, resource, id, secret);
        }
    }
}
