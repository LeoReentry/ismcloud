using IsmIoTPortal.Models;
using IsmIoTSettings;
using Microsoft.Azure.KeyVault;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Web;
using SharpCompress.Writers;
using SharpCompress.Common;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;

namespace IsmIoTPortal
{
    public class PortalUtils
    {
        public static async Task RolloutFwUpdateAsync(string device, ServiceClient serviceClient, string blobUrl)
        {
            // Method to invoke
            var methodInvokation = new CloudToDeviceMethod("firmwareUpdate");
            // Method payload
            var payload = JsonConvert.SerializeObject(new
            {
                blobUrl = blobUrl,
                fileName = blobUrl.Split('/').Last()
            });
            methodInvokation.SetPayloadJson(payload);
            // Invoke method on device
            var response = await serviceClient.InvokeDeviceMethodAsync(device, methodInvokation).ConfigureAwait(false);
        }

        public static async Task CreateNewFirmwareUpdateTask(HttpPostedFileBase file, string location, int id)
        {
            IsmIoTPortalContext db = new IsmIoTPortalContext();
            var software = db.Software.Find(id);
            try
            {
                var fileName = "update.tar";
                // Creates all directories in path that do not exist
                Directory.CreateDirectory(location);
                // Full path of file
                string filePath = Path.Combine(location, fileName);
                // Save file to disk
                file.SaveAs(filePath);
                // Update software status
                software.Status = "Saved";
                db.SaveChanges();

                // Calculate SHA 256 hash
                var checksum = await Sha256Sum(filePath).ConfigureAwait(false);
                // Get checksum string
                var checksum_string = BitConverter.ToString(checksum).Replace("-", String.Empty).ToLower();
                // Add checksum string to database
                software.Checksum = checksum_string;
                db.SaveChanges();

                // Get access to key vault
                var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
                // Sign Checksum
                var sig = await kv.SignAsync(
                    keyIdentifier: ConfigurationManager.AppSettings["kv:fw-signing-key"],
                    algorithm: Microsoft.Azure.KeyVault.WebKey.JsonWebKeySignatureAlgorithm.RS256,
                    digest: checksum).ConfigureAwait(false);

                // Save byte data as sig
                string checksumPath = Path.Combine(location, "sig");
                File.WriteAllBytes(checksumPath, sig.Result);
                software.Status = "Signed";
                db.SaveChanges();

                // Create tarball
                var tarball = CreateTarBall(location);
                software.Status = "Compressed";
                db.SaveChanges();

                // Upload
                var uri = await UploadToBlobStorage(Path.GetFileName(tarball), tarball).ConfigureAwait(false);
                if (uri.Equals("Error"))
                {
                    software.Status = "Error during upload.";
                    db.SaveChanges();
                }
                else
                {
                    software.Status = "Ready";
                    software.Url = uri;
                    db.SaveChanges();
                }

            }
            catch(Exception e)
            {
                software.Status = "Error";
                db.SaveChanges();
            }
        }

        public static async Task<byte[]> Sha256Sum(string filePath)
        {
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] sha256sum;
            // Buffered Calculation
            using (var bufferedStream = new BufferedStream(fileStream))
            {
                var sha = new SHA256Managed();
                sha256sum = sha.ComputeHash(bufferedStream);
            }
            return sha256sum;
        }

        private static async Task<string> GetToken(string authority, string resource, string scope)
        {
            var id = ConfigurationManager.AppSettings["hsma-ida:PortalClientId"];
            var secret = ConfigurationManager.AppSettings["hsma-ida:PortalAppKey"];
            return await IsmUtils.GetAccessToken(authority, resource, id, secret);
        }

        public static string GetPublicKey(Microsoft.Azure.KeyVault.WebKey.JsonWebKey key)
        {
            TextWriter outputStream = new StringWriter();
            outputStream.WriteLine("-----BEGIN PUBLIC KEY-----");
            var pkcs8Key = IsmUtils.ConvertJwkToPkcs8(key);
            for (Int32 i = 0; i < pkcs8Key.Length; i += 64)
            {
                outputStream.WriteLine(pkcs8Key.ToCharArray(), i, (Int32)Math.Min(64, pkcs8Key.Length - i));
            }
            outputStream.WriteLine("-----END PUBLIC KEY-----");
            return outputStream.ToString();
        }

        private static async Task<string> UploadToBlobStorage(string fileName, string filePath)
        {
            // Get reference to storage account
            var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["storageConnection"].ConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference(ConfigurationManager.ConnectionStrings["containerFirmware"].ConnectionString);
            await blobContainer.CreateIfNotExistsAsync();
            // Get reference to blob
            var blob = blobContainer.GetBlockBlobReference(fileName);
            // Upload BLOB (we don't need a SAS here since we're already authenticated)
            try
            {
                await blob.UploadFromFileAsync(filePath).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                return "Error";
            }
            return blob.Uri.ToString();
        }

        private static string CreateTarBall(string dir)
        {
            string tarPath = dir + ".tar";
            string targzPath = dir + ".tar.gz";
            using (Stream stream = File.OpenWrite(tarPath))
            using (var writer = WriterFactory.Open(stream, ArchiveType.Tar, new WriterOptions(CompressionType.None)))//Open(ArchiveType.Tar, stream))
            {
                writer.WriteAll(dir, "*", SearchOption.AllDirectories);
            }
            using (Stream stream = File.OpenWrite(targzPath))
            using (var writer = WriterFactory.Open(stream, ArchiveType.GZip, new WriterOptions(CompressionType.GZip)))
            {
                writer.Write("Tar.tar", tarPath);
            }
            // Delete tarfile
            File.Delete(tarPath);
            // Delete directory
            Directory.Delete(dir, true);
            return targzPath;
        }
    }
}