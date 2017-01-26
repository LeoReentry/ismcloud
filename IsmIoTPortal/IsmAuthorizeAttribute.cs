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

        public string Groups { get; set; }
        public string GroupObjectIds { get; set; }

        public IsmAuthorizeAttribute()
        {
            this._clientId = ConfigurationManager.AppSettings["ida:ClientId"];
            this._appKey = ConfigurationManager.AppSettings["ida:AppKey"];
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // First check if user is authenticated
            if (!ClaimsPrincipal.Current.Identity.IsAuthenticated)
                return false;
            else if (this.Groups == null && this.GroupObjectIds == null) // If there are no groups return here
                return base.AuthorizeCore(httpContext);

            List<string> groupsList = null;
            List<string> groupsObjectIdsList = new List<string>();

            // Split groups and group object ids into lists
            if (Groups != null)
            {
                // Remove spaces
                Groups = System.Text.RegularExpressions.Regex.Replace(Groups, " ", "");
                // Split by ,
                groupsList = Groups.Split(',').ToList();
            }
            if (GroupObjectIds != null)
            {
                // Remove spaces
                GroupObjectIds = System.Text.RegularExpressions.Regex.Replace(GroupObjectIds, " ", "");
                // Split by ,
                groupsObjectIdsList = GroupObjectIds.Split(',').ToList();
            }

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

                // If we have groups we need to convert to group IDs, enter here
                if (groupsList != null)
                {
                    // Query the Graph API to get all groups
                    var groups = activeDirectoryClient.Groups.ExecuteAsync().Result;
                    // Iterate over collection
                    do
                    {
                        // Get current page as list
                        List<IGroup> groupObjects = groups.CurrentPage.ToList();
                        // Select all object IDs that have a matching display name to our groups list
                        var idList = groupObjects
                            .Where(g => groupsList.Contains(g.DisplayName))
                            .Select(g => g.ObjectId);
                        // Add these IDs to our ID list
                        groupsObjectIdsList.AddRange(idList);
                    } while (groups != null && groups.MorePagesAvailable && !inGroup);
                }

                // If we have a group ID, check if the user is member of that group
                if (groupsObjectIdsList.Count > 0)
                {
                    // Get the user
                    var user = activeDirectoryClient.Users
                        .Where(u => u.ObjectId.Equals(userObjectId))
                        .ExecuteSingleAsync().Result;
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
                                if (groupsObjectIdsList.Contains(group.ObjectId))
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
                string message = string.Format("Unable to authorize AD user: {0} against group: {1}", ClaimsPrincipal.Current.Identity.Name, this.Groups);

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