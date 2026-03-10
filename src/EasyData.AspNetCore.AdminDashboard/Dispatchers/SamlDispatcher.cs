using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Saml;

using EasyData.AspNetCore.AdminDashboard.Authentication;
using EasyData.AspNetCore.AdminDashboard.Authentication.Storage;
using EasyData.AspNetCore.AdminDashboard.Routing;

namespace EasyData.AspNetCore.AdminDashboard.Dispatchers
{
    internal class SamlDispatcher : IDashboardDispatcher
    {
        private readonly string _action;

        public SamlDispatcher(string action)
        {
            _action = action;
        }

        public async Task DispatchAsync(AdminDashboardContext context, DashboardRouteMatch match)
        {
            switch (_action)
            {
                case "init":
                    HandleSamlInit(context);
                    break;
                case "callback":
                    await HandleSamlCallbackAsync(context);
                    break;
            }
        }

        private void HandleSamlInit(AdminDashboardContext context)
        {
            var options = context.Options;

            if (!options.EnableSaml || string.IsNullOrEmpty(options.SamlIdpSsoUrl))
            {
                context.HttpContext.Response.StatusCode = 404;
                return;
            }

            var authRequest = new AuthRequest(options.SamlIssuer, options.SamlAcsUrl);
            var redirectUrl = authRequest.GetRedirectUrl(options.SamlIdpSsoUrl);

            context.HttpContext.Response.Redirect(redirectUrl);
        }

        private async Task HandleSamlCallbackAsync(AdminDashboardContext context)
        {
            var options = context.Options;
            var httpContext = context.HttpContext;
            var ct = httpContext.RequestAborted;

            if (!options.EnableSaml || string.IsNullOrEmpty(options.SamlCertificate))
            {
                httpContext.Response.StatusCode = 404;
                return;
            }

            var form = await httpContext.Request.ReadFormAsync(ct);
            var samlResponseEncoded = form["SAMLResponse"].ToString();

            if (string.IsNullOrEmpty(samlResponseEncoded))
            {
                httpContext.Response.StatusCode = 401;
                await httpContext.Response.WriteAsync("Missing SAMLResponse.");
                return;
            }

            Response samlResponse;
            try
            {
                samlResponse = new Response(options.SamlCertificate, samlResponseEncoded);
            }
            catch
            {
                httpContext.Response.StatusCode = 401;
                await httpContext.Response.WriteAsync("Invalid SAMLResponse.");
                return;
            }

            if (!samlResponse.IsValid())
            {
                httpContext.Response.StatusCode = 401;
                await httpContext.Response.WriteAsync("SAML response validation failed.");
                return;
            }

            var username = samlResponse.GetNameID();
            if (string.IsNullOrEmpty(username))
            {
                httpContext.Response.StatusCode = 401;
                await httpContext.Response.WriteAsync("SAML response does not contain a NameID.");
                return;
            }

            var groupIds = samlResponse.GetCustomAttributeAsList(options.SamlGroupsAttribute)?
                .Select(g => g.Trim())
                .Where(g => !string.IsNullOrEmpty(g))
                .ToList() ?? new List<string>();

            // Create or update user and sync groups
            var authDbContext = httpContext.RequestServices.GetService(typeof(AuthDbContext)) as AuthDbContext;
            if (authDbContext == null)
            {
                httpContext.Response.StatusCode = 500;
                await httpContext.Response.WriteAsync("Auth database not available.");
                return;
            }

            var queries = new AuthStorageQueries(authDbContext);
            var userId = await queries.CreateOrUpdateSamlUserAsync(username, ct);
            await queries.SyncUserGroupsAsync(userId, groupIds, ct);

            // Set auth cookie
            var cookieService = httpContext.Items.TryGetValue("EasyData.CookieAuthService", out var service)
                ? service as AdminCookieAuthService
                : null;

            cookieService?.SetAuthCookie(httpContext, userId, username);

            httpContext.Response.Redirect(context.BasePath + "/");
        }
    }
}
