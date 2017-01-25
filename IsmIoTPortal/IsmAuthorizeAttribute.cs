using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace IsmIotPortal
{
    [AttributeUsageAttribute(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class IsmAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string _clientId = null;
        private readonly string _resourceId = null;
        private readonly string _appKey = null;
        private readonly string _graphResourceID = "https://graph.windows.net";


        public string AdGroup { get; set; }
        public string AdGroupObjectId { get; set; }

        public IsmAuthorizeAttribute()
        {
            this._clientId = ConfigurationManager.AppSettings["ida:PortalClientId"];
            this._resourceId = ConfigurationManager.AppSettings["ida:PortalResourceId"];
            this._appKey = ConfigurationManager.AppSettings["ida:PortalAppKey"];
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // First check if user is authenticated
            if (!ClaimsPrincipal.Current.Identity.IsAuthenticated)
                return false;
            else if (this.AdGroup == null && this.AdGroupObjectId == null) // If there are no groups return here
                return base.AuthorizeCore(httpContext);

            // Now check if user is in group by querying Azure AD Graph API using client
            bool inGroup = false;

            try
            {
                // Get information from user claim
                string signedInUserId = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
                string tenantId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;
                string userObjectId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;


                // Get AuthenticationResult for access token
                var clientCred = new ClientCredential(_clientId, _appKey);
                var authContext = new AuthenticationContext(string.Format("https://login.microsoftonline.com/{0}", tenantId));
                var authResult = authContext.AcquireToken(_resourceId, clientCred);

                // Create graph connection with our access token and API version
                ActiveDirectoryClient activeDirectoryClient = new ActiveDirectoryClient(
                    new Uri("https://graph.windows.net/" + tenantId),//this._resourceId),
                    async () => { return await this.GetToken(tenantId); }
                );

                // If we don't have a group id, we can quiery the graph API to find it
                if (this.AdGroupObjectId == null)
                {
                    var groups = activeDirectoryClient.Groups.ExecuteAsync().Result;
                    // Get our group
                    var group = activeDirectoryClient.Groups.Where(g => g.DisplayName.Equals(this.AdGroup)).ExecuteSingleAsync().Result;
                    if (group != null)
                    {
                        this.AdGroupObjectId = group.ObjectId;
                    }                    
                }

                if (this.AdGroupObjectId != null)
                {
                    var user = activeDirectoryClient.Users.Where(u => u.ObjectId.Equals(userObjectId)).ExecuteSingleAsync().Result;//graphConnection.IsMemberOf(this.AdGroupObjectId, userObjectId);
                    IUserFetcher retrievedUserFetcher = (User)user;
                    var groups = retrievedUserFetcher.MemberOf.ExecuteAsync().Result;
                    do
                    {
                        List<IDirectoryObject> directoryObjects = groups.CurrentPage.ToList();
                        foreach (IDirectoryObject directoryObject in directoryObjects)
                        {
                            if (directoryObject is Group)
                            {
                                Group group = directoryObject as Group;
                                if (group.ObjectId.Equals(this.AdGroupObjectId))
                                {
                                    inGroup = true;
                                    break;
                                }
                            }
                        }
                    } while (groups != null && groups.MorePagesAvailable && !inGroup);
                }


            }
            catch (Exception ex)
            {
                string message = string.Format("Unable to authorize AD user: {0} against group: {1}", ClaimsPrincipal.Current.Identity.Name, this.AdGroup);

                throw new Exception(message, ex);
            }

            return inGroup;
        }

        private async Task<string> GetToken(string tenantId)
        {
            // Get AuthenticationResult for access token
            var clientCred = new ClientCredential(_clientId, _appKey);
            var authContext = new AuthenticationContext(string.Format("https://login.windows.net/{0}", tenantId));
            var authResult = authContext.AcquireToken(_graphResourceID, clientCred);
            return authResult.AccessToken;
        }
    }
}