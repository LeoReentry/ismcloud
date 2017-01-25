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

namespace IsmIoTPortal
{
    [AttributeUsageAttribute(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class IsmAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string _clientId = null;
        private readonly string _appKey = null;
        private readonly string _graphResourceID = "https://graph.windows.net";


        public string Group { get; set; }
        public string GroupObjectId { get; set; }

        public IsmAuthorizeAttribute()
        {
            this._clientId = ConfigurationManager.AppSettings["ida:PortalClientId"];
            this._appKey = ConfigurationManager.AppSettings["ida:PortalAppKey"];
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // First check if user is authenticated
            if (!ClaimsPrincipal.Current.Identity.IsAuthenticated)
                return false;
            else if (this.Group == null && this.GroupObjectId == null) // If there are no groups return here
                return base.AuthorizeCore(httpContext);

            // Now check if user is in group by querying Azure AD Graph API using client
            bool inGroup = false;

            try
            {
                // Get information from user claim
                string signedInUserId = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
                string tenantId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;
                string userObjectId = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

                // Create graph connection
                ActiveDirectoryClient activeDirectoryClient = new ActiveDirectoryClient(
                    new Uri("https://graph.windows.net/" + tenantId),//this._resourceId),
                    async () => { return await this.GetToken(tenantId); }
                );

                // If we don't have a group id, we can query the graph API to find it
                if (this.GroupObjectId == null)
                {
                    // Get all groups
                    var groups = activeDirectoryClient.Groups.ExecuteAsync().Result;
                    // Find our group
                    var group = activeDirectoryClient.Groups.Where(g => g.DisplayName.Equals(this.Group)).ExecuteSingleAsync().Result;
                    // If the group exists, assign the ID
                    if (group != null)
                        this.GroupObjectId = group.ObjectId;
                }

                // If we have a group ID, check if the user is member of that group
                if (this.GroupObjectId != null)
                {
                    // Get the user
                    var user = activeDirectoryClient.Users.Where(u => u.ObjectId.Equals(userObjectId)).
                        ExecuteSingleAsync().Result;
                    // User Fetcher to get group information
                    IUserFetcher retrievedUserFetcher = (User)user;
                    // Get all objects that user is member of
                    var pagedCollection = retrievedUserFetcher.MemberOf.ExecuteAsync().Result;
                    // Iterate over collection
                    do
                    {
                        List<IDirectoryObject> directoryObjects = pagedCollection.CurrentPage.ToList();
                        foreach (IDirectoryObject directoryObject in directoryObjects)
                        {
                            // If the object is a group
                            if (directoryObject is Group)
                            {
                                Group group = directoryObject as Group;
                                // Check if that group is the group we're trying to authenticate against
                                // If so, set inGroup to true and exit loop
                                if (group.ObjectId.Equals(this.GroupObjectId))
                                {
                                    inGroup = true;
                                    break;
                                }
                            }
                        }
                    } while (pagedCollection != null && pagedCollection.MorePagesAvailable && !inGroup);
                }
            }
            catch (Exception ex)
            {
                string message = string.Format("Unable to authorize AD user: {0} against group: {1}", ClaimsPrincipal.Current.Identity.Name, this.Group);

                throw new Exception(message, ex);
            }

            return inGroup;
        }

        protected override void HandleUnauthorizedRequest(System.Web.Mvc.AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsAuthenticated)
            {
                var viewResult = new ViewResult
                {
                    ViewName = "~/Views/Account/Unauthorized.cshtml"
                };
                filterContext.Result = viewResult;
            }
            else
            {
                var viewResult = new ViewResult
                {
                    ViewName = "~/Views/Account/Login.cshtml"
                };
                filterContext.Result = viewResult;
            }
        }

        private async Task<string> GetToken(string tenantId)
        {
            // Get AuthenticationResult for access token
            var clientCred = new ClientCredential(_clientId, _appKey);
            var aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
            var authContext = new AuthenticationContext(aadInstance + tenantId);
            var authResult = await authContext.AcquireTokenAsync(_graphResourceID, clientCred);
            return authResult.AccessToken;
        }
    }
}