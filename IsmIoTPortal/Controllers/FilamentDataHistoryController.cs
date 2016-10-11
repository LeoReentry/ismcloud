using IsmIoTPortal.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace IsmIoTPortal.Controllers
{
    [Authorize]
    public class FilamentDataHistoryController : Controller
    {
        private IsmIoTPortalContext db = new IsmIoTPortalContext();

        // For PowerBI API access after /SignIn
        public AuthenticationResult AuthResult { get; set; }
        public string DashboardName { get; set; } = "ISM IoT Dashboard";
        public string BaseUri { get; set; } = "https://api.powerbi.com/beta/myorg/";

        // GET: FilamentDataHistory
        public ActionResult Index(string DeviceId)
        {
            // default interval when you visit Index of FilamentDataHistoryController
            // is e.g. the last Hour
            int hours = 24;
            DateTimeInterval defaultInterval = new DateTimeInterval();
            defaultInterval.DeviceId = DeviceId;
            defaultInterval.From = DateTime.UtcNow.Subtract(TimeSpan.FromHours(hours));
            defaultInterval.To = DateTime.UtcNow;

            defaultInterval.List = db.FilamentData.Where(d => d.DeviceId == DeviceId).Where(d => d.Time >= defaultInterval.From && d.Time <= defaultInterval.To).ToList<FilamentData>();

            return View(defaultInterval);
        }

        //Post: FilamentDataHistory/<interval>
        [HttpPost]
        public ActionResult Index(DateTimeInterval interval)
        {
            // query new filament data list with the posted interval
            var data = db.FilamentData.Where(d => d.DeviceId == interval.DeviceId).Where(d => d.Time >= interval.From && d.Time <= interval.To);
            interval.List = data.ToList<FilamentData>();
            return View(interval);
        }

        public ActionResult SignIn()
        {
            //Create a query string
            //Create a sign-in NameValueCollection for query string
            var @params = new System.Collections.Specialized.NameValueCollection
            {
                //Azure AD will return an authorization code. 
                //See the Redirect class to see how "code" is used to AcquireTokenByAuthorizationCode
                {"response_type", "code"},

                //Client ID is used by the application to identify themselves to the users that they are requesting permissions from. 
                //You get the client id when you register your Azure app.
                {"client_id", "04ea997e-f5f8-4c0e-b02c-498962f4fa1b"},

                //Resource uri to the Power BI resource to be authorized
                {"resource", "https://analysis.windows.net/powerbi/api"},

                //After user authenticates, Azure AD will redirect back to the web app
                // local
                //{"redirect_uri", "http://localhost:39860/FilamentDataHistory/Redirect"}
                // cloud
                // TODO: No hardcoded domain
                {"redirect_uri", IsmIoTSettings.Settings.webCompleteAddress + "/FilamentDataHistory/Redirect"}
            };

            //Create sign-in query string
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString.Add(@params);

            //Redirect authority
            //Authority Uri is an Azure resource that takes a client id to get an Access token
            string authorityUri = "https://login.windows.net/common/oauth2/authorize/";

            return new RedirectResult(String.Format("{0}?{1}", authorityUri, queryString));
        }

        public ActionResult Redirect()
        {
            //Redirect uri must match the redirect_uri used when requesting Authorization code.
            // local
            //string redirectUri = "http://localhost:39860/FilamentDataHistory/Redirect";
            // cloud
            // TODO: No hardcoded domain
            string redirectUri = IsmIoTSettings.Settings.webCompleteAddress + "/FilamentDataHistory/Redirect";
            string authorityUri = "https://login.windows.net/common/oauth2/authorize/";

            // Get the auth code
            string code = Request.Params.GetValues(0)[0];

            // Get auth token from auth code       
            TokenCache TC = new TokenCache();

            string ClientID = System.Configuration.ConfigurationManager.AppSettings.Get("ClientID");
            // Achtung der Schlüssel wird wirklich nur nach dem Speichern Angezeigt, bei erneuten Besuchen im Verwaltungsportal ist er "ausgesternt"
            string ClientSecret = System.Configuration.ConfigurationManager.AppSettings.Get("ClientSecret");
            AuthenticationContext AC = new AuthenticationContext(authorityUri, TC);
            ClientCredential cc = new ClientCredential
                (ClientID,
                ClientSecret);

            AuthenticationResult AR = AC.AcquireTokenByAuthorizationCode(code, new Uri(redirectUri), cc);

            //Set Session "authResult" index string to the AuthenticationResult
            Session["authResult"] = AR;

            // local
            //return new RedirectResult("http://localhost:39860/FilamentDataHistory/DataDashboard");
            // cloud
            // TODO: No hardcoded domain
            return new RedirectResult(IsmIoTSettings.Settings.webCompleteAddress + "/FilamentDataHistory/DataDashboard");
        }

        public ActionResult DataDashboard()
        {
            // Model for Power BI Dashboard Information
            PbiDashboardInfo dashboardInfo = new PbiDashboardInfo();

            //Test for AuthenticationResult
            if (Session["authResult"] != null)
            {
                //Get the authentication result from the session
                AuthResult = (AuthenticationResult)Session["authResult"];
           
                dashboardInfo.UserId = AuthResult.UserInfo.DisplayableId;
                dashboardInfo.AccessToken = AuthResult.AccessToken;
                dashboardInfo.TileEmbedURLs = new List<string>();

                // Get Dashboard
                PBIDashboard dashboard = GetDashboardByDisplayName(DashboardName);
                if(dashboard != null)
                {
                    dashboardInfo.DashboardId = dashboard.id;

                    // Get Tile
                    PBITile tile = GetTileByTitle("fcwindow", dashboard.id);          
                    dashboardInfo.TileEmbedURLs.Add(tile.embedUrl);

                    tile = GetTileByTitle("flwindow", dashboard.id);
                    dashboardInfo.TileEmbedURLs.Add(tile.embedUrl);

                    tile = GetTileByTitle("avgfc", dashboard.id);
                    dashboardInfo.TileEmbedURLs.Add(tile.embedUrl);

                    tile = GetTileByTitle("histo", dashboard.id);
                    dashboardInfo.TileEmbedURLs.Add(tile.embedUrl);
                }

            }
            else
            {
                // TODO:
                // Redirect auf Startseite oder Seite wo man SignIn machen kann 
            }

            //return View(model: AuthResult.AccessToken);
            return View(model: dashboardInfo);
        }

        private PBIDashboard GetDashboardByDisplayName(string displayName)
        {
            string responseContent = string.Empty;

            //Configure datasets request
            System.Net.WebRequest request = System.Net.WebRequest.Create(String.Format("{0}dashboards", BaseUri)) as System.Net.HttpWebRequest;
            request.Method = "GET";
            request.ContentLength = 0;
            request.Headers.Add("Authorization", String.Format("Bearer {0}", AuthResult.AccessToken));

            //Get datasets response from request.GetResponse()
            using (var response = request.GetResponse() as System.Net.HttpWebResponse)
            {
                //Get reader from response stream
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    responseContent = reader.ReadToEnd();
                    //Deserialize JSON string
                    PBIDashboards PBIDashboards = JsonConvert.DeserializeObject<PBIDashboards>(responseContent);

                    //Get each Dataset from 
                    foreach (PBIDashboard db in PBIDashboards.value)
                    {
                        if (db.displayName == displayName)
                        {
                            return db;
                        }
                    }
                }
            }

            // No Dashboard with displayName found
            return null;
        }

        private PBITile GetTileByTitle(string title, string dashboardId)
        {
            string responseContent = string.Empty;

            //Configure datasets request
            System.Net.WebRequest request = System.Net.WebRequest.Create(String.Format("{0}Dashboards/{1}/Tiles", BaseUri, dashboardId)) as System.Net.HttpWebRequest;
            request.Method = "GET";
            request.ContentLength = 0;
            request.Headers.Add("Authorization", String.Format("Bearer {0}", AuthResult.AccessToken));

            //Get datasets response from request.GetResponse()
            using (var response = request.GetResponse() as System.Net.HttpWebResponse)
            {
                //Get reader from response stream
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    responseContent = reader.ReadToEnd();

                    //Deserialize JSON string
                    PBITiles PBITiles = JsonConvert.DeserializeObject<PBITiles>(responseContent);

                    //Get each Dataset from 
                    foreach (PBITile tile in PBITiles.value)
                    {
                        if(tile.title == title)
                        {
                            return tile;
                        }
                    }
                }
            }

            // No Tile with title found
            return null;
        }

        //Power BI Datasets
        //
        public class PBIDashboards
        {
            public PBIDashboard[] value { get; set; }
        }
        public class PBIDashboard
        {
            public string id { get; set; }
            public string displayName { get; set; }
        }
        public class PBITiles
        {
            public PBITile[] value { get; set; }
        }
        public class PBITile
        {
            public string id { get; set; }
            public string title { get; set; }
            public string embedUrl { get; set; }
        }
    }
}