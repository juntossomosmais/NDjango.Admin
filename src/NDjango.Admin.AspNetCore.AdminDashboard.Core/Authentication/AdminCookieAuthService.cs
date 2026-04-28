using System;
using System.Text;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

using Newtonsoft.Json.Linq;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    internal class AdminCookieAuthService
    {
        private const string Purpose = "NDjango.Admin.AdminDashboard.Auth";
        private readonly IDataProtector _protector;
        private readonly string _cookieName;
        private readonly TimeSpan _expiration;

        public AdminCookieAuthService(StaticKeyDataProtectionProvider dataProtectionProvider, AdminDashboardOptions options)
        {
            _protector = dataProtectionProvider.CreateProtector(Purpose);
            _cookieName = options.CookieName;
            _expiration = options.CookieExpiration;
        }

        public void SetAuthCookie(HttpContext httpContext, string userId, string username)
        {
            var payload = new JObject
            {
                ["u"] = username,
                ["id"] = userId,
                ["t"] = DateTime.UtcNow.ToString("o")
            };

            var encrypted = _protector.Protect(payload.ToString(Newtonsoft.Json.Formatting.None));

            httpContext.Response.Cookies.Append(_cookieName, encrypted, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(_expiration),
                IsEssential = true
            });
        }

        public (string UserId, string Username)? ValidateCookie(HttpContext httpContext)
        {
            if (!httpContext.Request.Cookies.TryGetValue(_cookieName, out var cookieValue))
                return null;

            try {
                var decrypted = _protector.Unprotect(cookieValue);
                var payload = JObject.Parse(decrypted);

                var username = payload["u"]?.Value<string>();
                var userId = payload["id"]?.Value<string>();
                var timestamp = payload["t"]?.Value<DateTime>() ?? DateTime.MinValue;

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(userId))
                    return null;

                if (DateTime.UtcNow - timestamp > _expiration)
                    return null;

                return (userId, username);
            }
            catch {
                return null;
            }
        }

        public void ClearAuthCookie(HttpContext httpContext)
        {
            httpContext.Response.Cookies.Delete(_cookieName);
        }
    }
}
