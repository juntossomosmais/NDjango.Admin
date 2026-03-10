using System.Threading.Tasks;

using EasyData.AspNetCore.AdminDashboard.Authentication;
using EasyData.AspNetCore.AdminDashboard.Authentication.Storage;
using EasyData.AspNetCore.AdminDashboard.Routing;
using EasyData.AspNetCore.AdminDashboard.ViewModels;

namespace EasyData.AspNetCore.AdminDashboard.Dispatchers
{
    internal class AuthDispatcher : IDashboardDispatcher
    {
        private readonly string _action;

        public AuthDispatcher(string action)
        {
            _action = action;
        }

        public async Task DispatchAsync(AdminDashboardContext context, DashboardRouteMatch match)
        {
            switch (_action)
            {
                case "login":
                    await HandleLoginAsync(context);
                    break;
                case "login_post":
                    await HandleLoginPostAsync(context);
                    break;
                case "logout":
                    HandleLogout(context);
                    break;
            }
        }

        private async Task HandleLoginAsync(AdminDashboardContext context)
        {
            var nextUrl = context.HttpContext.Request.Query["next"].ToString();
            var model = new LoginViewModel
            {
                Title = context.Options.DashboardTitle,
                BasePath = context.BasePath,
                NextUrl = nextUrl,
                EnableSaml = context.Options.EnableSaml
            };
            await ViewRenderer.RenderLoginViewAsync(context.HttpContext, model);
        }

        private async Task HandleLoginPostAsync(AdminDashboardContext context)
        {
            var ct = context.HttpContext.RequestAborted;
            var form = await context.HttpContext.Request.ReadFormAsync(ct);
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var nextUrl = form["next"].ToString();

            var authDbContext = context.HttpContext.RequestServices.GetService(typeof(AuthDbContext)) as AuthDbContext;
            var queries = new AuthStorageQueries(authDbContext);
            var user = await queries.GetUserByUsernameAsync(username, ct);

            if (user == null || !user.Value.IsActive || !PasswordHasher.VerifyPassword(password, user.Value.PasswordHash))
            {
                var model = new LoginViewModel
                {
                    Title = context.Options.DashboardTitle,
                    BasePath = context.BasePath,
                    ErrorMessage = "Please enter the correct username and password.",
                    NextUrl = nextUrl,
                    EnableSaml = context.Options.EnableSaml
                };
                await ViewRenderer.RenderLoginViewAsync(context.HttpContext, model);
                return;
            }

            await queries.UpdateLastLoginAsync(user.Value.Id, ct);

            var cookieService = GetCookieAuthService(context);
            cookieService?.SetAuthCookie(context.HttpContext, user.Value.Id, user.Value.Username);

            var redirectUrl = string.IsNullOrEmpty(nextUrl) ? context.BasePath + "/" : nextUrl;
            context.HttpContext.Response.Redirect(redirectUrl);
        }

        private void HandleLogout(AdminDashboardContext context)
        {
            var cookieService = GetCookieAuthService(context);
            cookieService?.ClearAuthCookie(context.HttpContext);
            context.HttpContext.Response.Redirect(context.BasePath + "/login/");
        }

        private static AdminCookieAuthService GetCookieAuthService(AdminDashboardContext context)
        {
            if (context.HttpContext.Items.TryGetValue("EasyData.CookieAuthService", out var service))
                return service as AdminCookieAuthService;
            return null;
        }
    }
}
