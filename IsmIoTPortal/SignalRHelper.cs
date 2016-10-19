using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace IsmIoTPortal
{
    /// <summary>
    /// Helper class that manages authentication on SignalR Hub and exposes the connection.
    /// </summary>
    public class SignalRHelper
    {
        // Constructor
        public SignalRHelper()
        {
            // Authentication
            authority = aadInstance + tenant;
            authContext = new AuthenticationContext(authority);
            clientCredential = new ClientCredential(clientId, appKey);
            // Initialize asynchronously so that no thread is blocked
            Init();
        }

        #region fields, properties
        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The App Key is a credential used by the application to authenticate to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Authority is the sign-in URL of the tenant.
        //
        private readonly string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private readonly string tenant = ConfigurationManager.AppSettings["ida:TenantId"];
        private readonly string clientId = ConfigurationManager.AppSettings["ida:DeviceControllerClientId"];
        private readonly string appKey = ConfigurationManager.AppSettings["ida:DeviceControllerAppKey"];
        private string authority = "";
        //
        // To authenticate for SignalR, the client needs to know the service's App ID URI.
        //
        private string portalResourceId = ConfigurationManager.AppSettings["ida:PortalResourceId"];
        private readonly AuthenticationContext authContext = null;
        private readonly ClientCredential clientCredential = null;

        // Variables for public access
        // Authentication result
        private AuthenticationResult authResult = null;
        // Authentication complete
        public bool Authenticated = false;
        // SignalR 
        public HubConnection SignalRHubConnection = null;
        public IHubProxy SignalRHubProxy = null;

        #endregion

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
                    result = await authContext.AcquireTokenAsync(portalResourceId, clientCredential);
                    Console.WriteLine("nop");
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
            authResult = result;
            return true;
        }

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
                SignalRHubConnection.Headers.Add("Authorization", "Bearer " + authResult.AccessToken);
                SignalRHubConnection.Headers.Add("Bearer", authResult.AccessToken);

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
    }
}