using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                            publicKeyWriter.Write(keyValuesStream.GetBuffer());
                        }
                        #endregion
                        // keyValues SEQUENCE is done

                        // Finishing PublicKey BIT STRING
                        // Length of keyValues SEQUENCE into PublicKey BIT STRING
                        WriteLengthDer(publicKeyDataWriter, (ulong)publicKeyStream.Length);
                        // Write content keyValues SEQUENCE into PublicKey BIT STRING
                        publicKeyDataWriter.Write(publicKeyStream.GetBuffer());
                    }
                    #endregion

                    WriteLengthDer(DERWriter, (ulong)publicKeyDataStream.Length);
                    DERWriter.Write(publicKeyDataStream.GetBuffer());
                }
                #endregion

                var base64String = Convert.ToBase64String(s.GetBuffer());
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
                    if ((length >> i*4) != 0)
                    {
                        l = i + 1;
                        break;
                    }
                }
                // Write long form and number of bytes
                w.Write((byte)(0x80 + l));
                // Write all bytes
                while (l >= 0)
                    w.Write((byte)(length >> (l-- - 1) & 0xFF));
            }
        }
    }
}
