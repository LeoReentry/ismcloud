using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
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
    }
}
