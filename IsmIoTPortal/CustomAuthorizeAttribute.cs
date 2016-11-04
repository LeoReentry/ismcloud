

using System.Web.Mvc;

namespace IsmIoTPortal
{
    /// <summary>
    /// This custom AuthorizeAttribute will render a page asking users to log in if they aren't authenticated.
    /// </summary>
    public class CustomAuthorizeAttribute : AuthorizeAttribute
    {
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var viewResult = new ViewResult
            {
                ViewName = "~/Views/Account/Login.cshtml"
            };
            filterContext.Result = viewResult;
        }
    }
}