using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures
{
    internal static class AdminLoginHelper
    {
        public const string DefaultUsername = "admin";
        public const string DefaultPassword = "admin";

        public static async Task WaitForReadinessAsync(IHost host, TimeSpan? timeout = null)
        {
            var readiness = host.Services.GetRequiredService<AuthBootstrapReadinessState>();
            using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(60));
            await readiness.WaitForReadyAsync(cts.Token);
        }

        public static async Task<string> LoginAndGetCookieAsync(
            IHost host, string username = DefaultUsername, string password = DefaultPassword)
        {
            var client = host.GetTestClient();
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
            });

            var response = await client.PostAsync("/admin/login/", formContent);
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var header in cookies)
                {
                    if (header.Contains(".NDjango.Admin.Auth"))
                        return header.Split(';')[0];
                }
            }
            return null;
        }

        public static HttpClient CreateAuthenticatedClient(IHost host, string authCookie)
        {
            var client = host.GetTestClient();
            if (!string.IsNullOrEmpty(authCookie))
                client.DefaultRequestHeaders.Add("Cookie", authCookie);
            return client;
        }
    }
}
