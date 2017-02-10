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
        /// <summary>
        /// This function rolls out a firmware update to a specified device asynchronously. Call without using await.
        /// </summary>
        /// <param name="device">Device ID.</param>
        /// <param name="serviceClient">Service Client used to call direct methods.</param>
        /// <param name="blobUrl">Url to blob where firmware update is located. Must be in container fwupdates</param>
        /// <returns></returns>
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

        /// <summary>
        /// Creates a new firmware update ready to be rolled out. Call asynchronously because it can take some time. Don't use await.
        /// </summary>
        /// <param name="file">File of firmware update. Must be a tar.</param>
        /// <param name="location">Directory of where file will be stored on server.</param>
        /// <param name="id">Key of software version in databse.</param>
        /// <returns></returns>
        public static async Task CreateNewFirmwareUpdateTask(string fileUrl, string location, int id)
        {
            IsmIoTPortalContext db = new IsmIoTPortalContext();
            // Get reference to software
            var software = db.Releases.Find(id);
            try
            {
                // Filename is always the same
                var fileName = "update.tar";
                // Creates all directories in path that do not exist
                Directory.CreateDirectory(location);
                // Full path of file
                string filePath = Path.Combine(location, fileName);
                // Save file to disk
                var retval = await DownloadFromBlobStorage(fileUrl, filePath).ConfigureAwait(false);
                if (retval.Equals("Error"))
                {
                    // Update software status
                    software.Status = "Error during download of file.";
                    db.SaveChanges();
                    return;
                }
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
                    // Key ID is in appsettings
                    keyIdentifier: ConfigurationManager.AppSettings["kv:fw-signing-key"],
                    // Sign with RS256
                    algorithm: Microsoft.Azure.KeyVault.WebKey.JsonWebKeySignatureAlgorithm.RS256,
                    // We want to sign the checksum
                    digest: checksum).ConfigureAwait(false);

                // Save byte data as sig
                string checksumPath = Path.Combine(location, "sig");
                File.WriteAllBytes(checksumPath, sig.Result);
                software.Status = "Signed";
                db.SaveChanges();

                // Create tarball (.tar.gz file containing signed checksum and tarfile with update)
                var tarball = CreateTarBall(location);
                software.Status = "Compressed";
                db.SaveChanges();

                // Upload
                var uri = await UploadToBlobStorage(Path.GetFileName(tarball), tarball).ConfigureAwait(false);
                if (uri.Equals("Error"))
                {
                    software.Status = "Error during upload.";
                    db.SaveChanges();
                    return;
                }
                // Remove old file from BLOB storage
                var retVal = RemoveFromBlobStorage(fileUrl);
                if (retval.Equals("Error"))
                {
                    software.Status = "Error during removal of old file from BLOB storage.";
                    db.SaveChanges();
                    return;
                }
                // Everything is ok
                software.Status = "Ready";
                software.Url = uri;
                db.SaveChanges();

            }
            catch(Exception e)
            {
                software.Status = "Error";
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Calculates SHA256 sum of a file.
        /// </summary>
        /// <param name="filePath">Path to file.</param>
        /// <returns>Byte array of checksum.</returns>
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

        /// <summary>
        /// GetToken function used to authenticate against Key Vault
        /// </summary>
        /// <param name="authority"></param>
        /// <param name="resource"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        private static async Task<string> GetToken(string authority, string resource, string scope)
        {
            var id = ConfigurationManager.AppSettings["hsma-ida:PortalClientId"];
            var secret = ConfigurationManager.AppSettings["hsma-ida:PortalAppKey"];
            return await IsmUtils.GetAccessToken(authority, resource, id, secret);
        }

        /// <summary>
        /// Get public key as a PEM formatted base64 encoded string.
        /// </summary>
        /// <param name="key">Key object to be formatted.</param>
        /// <returns>PEM formatted string.</returns>
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

        /// <summary>
        /// Uploads a file to the BLOB storage to container 'fwupdates'.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="filePath">Path where it is located.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Downloads a file from the BLOB storage from container 'fwupdates'.
        /// </summary>
        /// <param name="blobUri">BLOB Uri of file to be downloaded.</param>
        /// <param name="filePath">Path where it is located.</param>
        /// <returns>Filepath when successful, "Error" when not.</returns>
        private static async Task<string> DownloadFromBlobStorage(string blobUri, string filePath)
        {
            try
            {
                // Get reference to storage account
                var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["storageConnection"].ConnectionString);
                var blobClient = storageAccount.CreateCloudBlobClient();
                // Get reference to blob
                var blob = await blobClient.GetBlobReferenceFromServerAsync(new Uri(blobUri));
                // Download BLOB (we don't need a SAS here since we're already authenticated)
                blob.DownloadToFile(filePath, FileMode.Create);
            }
            catch (Exception e)
            {
                return "Error";
            }
            return filePath;
        }

        /// <summary>
        /// Removes a file from the BLOB storage container 'fwupdates'.
        /// </summary>
        /// <param name="blobUri">BLOB Uri of file to be removed.</param>
        /// <returns>Empy string when successful, "Error" when not.</returns>
        private static async Task<string> RemoveFromBlobStorage(string blobUri)
        {
            try
            {
                // Get reference to storage account
                var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["storageConnection"].ConnectionString);
                var blobClient = storageAccount.CreateCloudBlobClient();
                // Get reference to blob
                var blob = await blobClient.GetBlobReferenceFromServerAsync(new Uri(blobUri));
                // Delete BLOB
                await blob.DeleteAsync();
            }
            catch (Exception e)
            {
                return "Error";
            }
            return "";
        }

        /// <summary>
        /// Creates .tar.gz file of a directory.
        /// </summary>
        /// <param name="dir">Path of directory.</param>
        /// <returns>Path of tarball.</returns>
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