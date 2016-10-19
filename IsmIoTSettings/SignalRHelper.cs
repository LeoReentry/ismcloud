using System;
using System.Configuration;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Bson;

namespace IsmIoTSettings
{
    /// <summary>
    /// Helper class that manages authentication on SignalR Hub and exposes the connection.
    /// </summary>
    public class SignalRHelper
    {
        // Constructor
        public SignalRHelper(string name)
        {
            // Authentication
            // The Authority is the sign-in URL of the tenant.
            var authority = _aadInstance + _tenant;
            _authContext = new AuthenticationContext(authority);
            // The Client ID is used by the application to uniquely identify itself to Azure AD.
            // The App Key is a credential used by the application to authenticate to Azure AD.
            string clientId = ConfigurationManager.AppSettings["ida:" + name + "ClientId"];
            string appKey = ConfigurationManager.AppSettings["ida:" + name + "AppKey"];
            _clientCredential = new ClientCredential(clientId, appKey);
            // Initialize asynchronously so that no thread is blocked
            Init().ContinueWith(t =>
            {
                if (t.Exception != null) Console.WriteLine(t.Exception);
                Initialized = t.Result;
            });
        }

        #region fields and properties
        //
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        //
        private readonly string _aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private readonly string _tenant = ConfigurationManager.AppSettings["ida:TenantId"];
        //
        // To authenticate for SignalR, the client needs to know the service's App ID URI.
        //
        private readonly string _portalResourceId = ConfigurationManager.AppSettings["ida:PortalResourceId"];
        private readonly AuthenticationContext _authContext;
        private readonly ClientCredential _clientCredential;
        private AuthenticationResult _authResult;

        // Variables for public access
        // Authentication result
        // Authentication complete
        public bool Initialized { get; private set; } = false;
        // SignalR 
        public HubConnection SignalRHubConnection { get; private set; }
        public IHubProxy SignalRHubProxy { get; private set; }

        #endregion

        #region methods
        /// <summary>
        /// This function will authenticate IsmDevicesController in AAD and initialize SignalR if so.
        /// </summary>
        /// <returns>True, if authentication was successful</returns>
        private async Task<bool> Init()
        {
            var success = await Authenticate();
            if (success)
                InitSignalR();
            return success;
        }

        /// <summary>
        /// This functions tries to authenticate the WorkerRole on the IoT Portal. That way it can access SignalR
        /// </summary>
        /// <returns>True for success, false for failure</returns>
        private async Task<bool> Authenticate()
        {
            //
            // Get an access token from Azure AD using client credentials.
            // If the attempt to get a token fails because the server is unavailable, retry twice after 3 seconds each.
            //
            AuthenticationResult result = null;
            var retryCount = 0;
            var retry = false;

            do
            {
                retry = false;
                try
                {
                    // ADAL includes an in memory cache, so this call will only send a message to the server if the cached token is expired.
                    result = await _authContext.AcquireTokenAsync(_portalResourceId, _clientCredential);
                }
                catch (AdalException ex)
                {
                    if (ex.ErrorCode == "temporarily_unavailable")
                    {
                        retry = true;
                        retryCount++;
                        Thread.Sleep(3000);
                    }

                }
            } while (retry && (retryCount < 3));

            if (result == null)
                return false;

            // If authentication was successful, save the authentication result to private field, so it can be read later
            _authResult = result;
            return true;
        }

        /// <summary>
        /// Initializes SignalR connection to Hub only if not yet initialized.
        /// </summary>
        private void InitSignalR()
        {
            // Nur einmal für die Webseiten dieser Web App Instanz eine SignalR Hub Connection + Proxy anlegen (wird erkannt, wenn noch null ist)
            if (SignalRHubConnection == null)
            {
                // local
                //signalRHubConnection = new HubConnection("http://localhost:39860/");
                // cloud
                // TODO: No hardcoded domain
                SignalRHubConnection = new HubConnection(IsmIoTSettings.Settings.webCompleteAddress);
                // Add authentication token to headers
                SignalRHubConnection.Headers.Add("Authorization", "Bearer " + _authResult.AccessToken);
                SignalRHubConnection.Headers.Add("Bearer", _authResult.AccessToken);

                SignalRHubProxy = SignalRHubConnection.CreateHubProxy("DashboardHub");

                // Connect
                SignalRHubConnection.Start().ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        t.Exception.Handle(e =>
                        {
                            Console.WriteLine(e.Message);
                            return true;
                        });
                    }
                    else
                    {
                        //Console.WriteLine("Verbindung aufgebaut!");
                    }
                }).Wait();
            }

        }

        /// <summary>
        /// Updates JWT and SignalR headers with new JWT.
        /// </summary>
        private async Task<bool> UpdateToken()
        {
            var success = await Authenticate();
            if (success)
            {
                SignalRHubConnection.Headers["Authorization"] = "Bearer " + _authResult.AccessToken;
                SignalRHubConnection.Headers["Bearer"] = _authResult.AccessToken;
            }
            return success;
        }

        /// <summary>
        /// Checks if the current JWT is still valid for authentication.
        /// </summary>
        /// <returns>True if valid, false if expired.</returns>
        private bool IsTokenValid()
        {
            return DateTime.UtcNow < _authResult.ExpiresOn.AddMinutes(-1).UtcDateTime;
        }

        /// <summary>
        /// Checks if SignalR Client is authenticated and authenticates if it isn't. Updates Token if expired.
        /// </summary>
        /// <returns>True for success.</returns>
        public async Task<bool> CheckAuthTask()
        {
            // If not authenticated, try to initialize again
            if (!Initialized) return await Init();
            // Update Headers if necessary
            if (!IsTokenValid()) return await UpdateToken();
            // If everything is fine, return true
            return true;
        }

        /// <summary>
        /// Invokes IsmDevicesIndexChanged only if client is properly authenticated.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task<bool> IsmDevicesIndexChangedTask()
        {
            var isAuthorized = await CheckAuthTask();
            if (isAuthorized)
                await SignalRHubProxy.Invoke<string>("IsmDevicesIndexChanged").ContinueWith(t => { });
            return isAuthorized;
        }

        /// <summary>
        /// Invokes DataForDashboard only if client is properly authenticated.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task<bool> DataDorDashboardTask(string deviceId, string fC, string fL, string imgUri, string colUri)
        {
            var isAuthorized = await CheckAuthTask();
            if (isAuthorized)
                await SignalRHubProxy.Invoke<string>("DataForDashboard", deviceId, imgUri, fC, fL, colUri).ContinueWith(t => { });
            return isAuthorized;
        }

        /// <summary>
        /// Invokes ValuesForDashboardControls only if client is properly authenticated.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task<bool> ValuesForDashboardControlsTask(string deviceId, int capturePeriod, double varThresh, double distMapThresh, double rGThresh, double restrFillThres, double dilVal)
        {
            var isAuthorized = await CheckAuthTask();
            if (isAuthorized)
                await SignalRHubProxy.Invoke<string>("DataForDashboard", deviceId, capturePeriod, varThresh, distMapThresh, rGThresh, restrFillThres, dilVal).ContinueWith(t => { });
            return isAuthorized;
        }
        #endregion
    }
}
;