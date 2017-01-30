using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace IsmIoTSettings
{
    /// <summary>
    /// Provides helper functions across the project
    /// </summary>
    public class IsmUtils
    {
        /// <summary>
        /// Get an JWT acess token asynchronously. 
        /// </summary>
        /// <param name="authority">Authority to authenticate against.</param>
        /// <param name="resourceId">ID of the resource you want access to.</param>
        /// <param name="clientId">Client ID of the client that's authenticating.</param>
        /// <param name="clientSecret">Secret of the client that's authenticating.</param>
        /// <returns></returns>
        public static async Task<string> GetAccessToken(string authority, string resourceId, string clientId, string clientSecret)
        {
            var authContext = new AuthenticationContext(authority);
            var credentials = new ClientCredential(clientId, clientSecret);
            var result = await authContext.AcquireTokenAsync(resourceId, credentials);
            if (result == null)
                throw new InvalidOperationException("Failed to obtain Token");
            return result.AccessToken;
        }

        /// <summary>
        /// Accepts a JSON Web Key public RSA key and converts it to a PKCS8 compatible DER encoded base64 encoded string.
        /// </summary>
        /// <param name="key">Key to convert</param>
        /// <returns>Return string.</returns>
        public static string ConvertJwkToPkcs8(Microsoft.Azure.KeyVault.WebKey.JsonWebKey key) 
        {
            /*
             * ASN.1 syntax using DER structure
             * 
             * Public key file in PKCS#8:
             * -----BEGIN PUBLIC KEY-----
             * BASE64 ENCODED DATA
             * -----END PUBLIC KEY-----
             * 
             * Encoded data in DER structure:
             * 
             * PublicKeyData = SEQUENCE {
             *  algorithmId AlgorithmID,
             *  publicKey   PublicKey
             * }
             * 
             * AlgorithmID = SEQUENCE {
             *  algorithm   OBJECT IDENTIFIER
             *  parameters  [Optional Data]
             * }
             * 
             * PublicKey = BIT STRING {
             *  keyValues   KeyValues
             * }
             * 
             * KeyValues = SEQUENCE {
             *  MODULUS     INTEGER
             *  EXPONENT    INTEGER
             * }
             *
             */
            // 
            // Public key file PKCS8#8:
            // -----BEGIN PUBLIC KEY-----
            //
            //
            using (var s = new MemoryStream())
            {
                var DERWriter = new BinaryWriter(s);

                // Begin
                // PublicKeyData : SEQUENCE
                DERWriter.Write((byte)0x30);

                #region Write PublicKeyData SEQUENCE into writer
                // Build inner sequence before we write length
                using (var publicKeyDataStream = new MemoryStream())
                {
                    var publicKeyDataWriter = new BinaryWriter(publicKeyDataStream);

                    // Begin
                    // AlgorithmId: SEQUENCE
                    // Start of sequence
                    publicKeyDataWriter.Write((byte)0x30);

                    #region Write AlgorithmID SEQUENCE into PublicKeyData SEQUENCE
                    {
                        // Write length of 13 bytes (this sequence is always the same in our case)
                        publicKeyDataWriter.Write((byte)0x0d);
                        
                        // Begin
                        // algorithm: OBJECT IDENTIFIER
                        publicKeyDataWriter.Write((byte)0x06);
                        // Length of OID is 9 bytes
                        publicKeyDataWriter.Write((byte)0x09);
                        // Write RSA key OID:
                        // OID is 1.2.840.113549.1.1.1
                        publicKeyDataWriter.Write(new byte[] {
                            0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01
                        });

                        // Begin 
                        // parameters: [Optional Value]
                        // We don't have any parameters, so we write NULL
                        // Null identifier
                        publicKeyDataWriter.Write((byte)0x05);
                        // Null byte
                        publicKeyDataWriter.Write((byte)0x00);
                    }
                    #endregion

                    // Begin
                    // PublicKey: BIT STRING
                    // Identifier of bit string
                    publicKeyDataWriter.Write((byte)0x03);

                    #region Write PublicKey BIT STRING into PublicKeyData SEQUENCE
                    // Bit String Stream
                    using(var publicKeyStream = new MemoryStream())
                    {
                        var publicKeyWriter = new BinaryWriter(publicKeyStream);
                        // Number of unused bits: 0
                        publicKeyWriter.Write((byte)0x00);

                        // Begin
                        // KeyValues: SEQUENCE
                        publicKeyWriter.Write((byte)0x30);
                        #region Write keyValues SEQUENCE into PublicKey BIT STRING
                        using (var keyValuesStream = new MemoryStream())
                        {
                            var keyValuesWriter = new BinaryWriter(keyValuesStream);

                            #region MODULUS
                            // Begin
                            // Modulus: INTEGER
                            WriteIntegerDer(keyValuesWriter, key.N);
                            #endregion
                            #region EXPONENT
                            // Begin
                            // Exponent: INTEGER
                            WriteIntegerDer(keyValuesWriter, key.E);
                            #endregion

                            // Integerwriter (keyValues) is done.
                            // Write length of keyValues
                            WriteLengthDer(publicKeyWriter, (ulong)keyValuesStream.Length);
                            // Write keyValues SEQUENCE
                            publicKeyWriter.Write(keyValuesStream.GetBuffer(), 0, (int)keyValuesStream.Length);
                        }
                        #endregion
                        // keyValues SEQUENCE is done

                        // Finishing PublicKey BIT STRING
                        // Length of keyValues SEQUENCE into PublicKey BIT STRING
                        WriteLengthDer(publicKeyDataWriter, (ulong)publicKeyStream.Length);
                        // Write content keyValues SEQUENCE into PublicKey BIT STRING
                        publicKeyDataWriter.Write(publicKeyStream.GetBuffer(), 0, (int)publicKeyStream.Length);
                    }
                    #endregion

                    WriteLengthDer(DERWriter, (ulong)publicKeyDataStream.Length);
                    DERWriter.Write(publicKeyDataStream.GetBuffer(), 0, (int)publicKeyDataStream.Length);
                }
                #endregion

                var base64String = Convert.ToBase64String(s.GetBuffer(), 0, (int)s.Length);
                return base64String;
            }
        }

        /// <summary>
        /// Writes a DER encoded integer to the specified writer. Is always unsigned.
        /// </summary>
        /// <param name="s">Writer to write to.</param>
        /// <param name="value">Integer value to write in bytes.</param>
        private static void WriteIntegerDer(BinaryWriter w, byte[] value)
        {
            // Begin
            // INTEGER
            w.Write((byte)0x02);

            ulong length = (ulong)value.Length;
            // If integer starts with empty bytes, we skip them
            int i = 0;
            while(value[i] == 0)
            {
                length--;
                i++;
            }

            // If highest bit is 1, we need to add an extra 0x00 byte because
            // DER integers are always signed but we write unsigned values
            if ((value[i] & 0x80) == 0x80)
            {
                WriteLengthDer(w, ++length);
                w.Write((byte)0x00);
            }
            else
                WriteLengthDer(w, length);

            // Now write integer
            while(i < value.Length)
                w.Write(value[i++]);

        }

        /// <summary>
        /// Writes DER encoded length to the specified writer.
        /// </summary>
        /// <param name="w">Writer to write to.</param>
        /// <param name="length">Length to write.</param>
        private static void WriteLengthDer(BinaryWriter w, ulong length)
        {
            // Short format length
            if (length < 0x80)
                w.Write((byte)length);
            // Long form
            else
            {
                // Calculate number of bytes necessary to display the length
                // Maximum length is 1
                int l = 1;
                for (int i = 7; i >= 0; i--)
                {
                    if ((length >> i * 8) != 0)
                    {
                        l = i + 1;
                        break;
                    }
                }
                // Write long form and number of bytes
                w.Write((byte)(0x80 + l));
                // Write all bytes
                while (l > 0)
                    w.Write((byte)(length >> ((--l) * 8) & 0xFF));
            }
        }

        public static class SoftwareUtils
        {
            public static async Task CreateNewFirmwareUpdateTask(HttpPostedFileBase file, string location)
            {
                // Creates all directories in path that do not exist
                Directory.CreateDirectory(location);
                // Full path of file
                string filePath = Path.Combine(location, Path.GetFileName(file.FileName));
                // Save file to disk
                file.SaveAs(filePath);
                // Calculate SHA 256 hash
                var checksum = await Sha256Sum(filePath);
                // Get access to key vault
                var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(GetToken));
                // Get key
                var key = await kv.GetKeyAsync(ConfigurationManager.AppSettings["kv:fw-signing-key"]);
                // Sign Checksum
                var sig = await kv.SignAsync(
                    keyIdentifier: ConfigurationManager.AppSettings["kv:fw-signing-key"],
                    algorithm: Microsoft.Azure.KeyVault.WebKey.JsonWebKeySignatureAlgorithm.RS256,
                    digest: checksum);
                // Save public key to disk
                var keyPath = Path.Combine(location, "public.pem");
                var publicKey = GetPublicKey(key.Key);
                File.WriteAllText(keyPath, publicKey);
                // Save byte data as sig
                string checksumPath = Path.Combine(location, "sig");
                File.WriteAllBytes(checksumPath, sig.Result);                
            }

            public static async Task<byte[]> Sha256Sum(string filePath)
            {
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                byte[] sha256sum;
                // Buffered Calculation
                using(var bufferedStream = new BufferedStream(fileStream))
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
                var pkcs8Key = ConvertJwkToPkcs8(key);
                for (Int32 i = 0; i < pkcs8Key.Length; i += 64)
                {
                    outputStream.WriteLine(pkcs8Key.ToCharArray(), i, (Int32)Math.Min(64, pkcs8Key.Length - i));
                }
                outputStream.WriteLine("-----END PUBLIC KEY-----");
                return outputStream.ToString();
            }


        }
    }
}
