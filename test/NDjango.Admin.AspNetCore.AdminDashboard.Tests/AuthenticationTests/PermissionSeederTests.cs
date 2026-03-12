using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    public class PermissionSeederTests : IClassFixture<AuthEnabledFixture>
    {
        private readonly HttpClient _client;

        public PermissionSeederTests(AuthEnabledFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task GetDashboard_Authenticated_ShowsAuthGroupAsync()
        {
            // Arrange
            var cookie = await LoginAndGetCookieAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/");
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Authentication and Authorization", content);
        }

        [Fact]
        public async Task GetPermissionList_AsSuperuser_ShowsUserEntityPermissionsAsync()
        {
            // Arrange
            var cookie = await LoginAndGetCookieAsync();
            var request = new HttpRequestMessage(HttpMethod.Get, "/admin/AuthPermission/");
            request.Headers.Add("Cookie", cookie);

            // Act
            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("add_category", content);
            Assert.Contains("view_category", content);
            Assert.Contains("change_category", content);
            Assert.Contains("delete_category", content);
        }

        [Fact]
        public async Task GetPermissionList_AsSuperuser_ShowsAuthEntityPermissionsAsync()
        {
            // Arrange
            var cookie = await LoginAndGetCookieAsync();

            // Act — fetch pages until the target permissions are found or no more results
            var content = "";
            for (var page = 1; page <= 5; page++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"/admin/AuthPermission/?page={page}");
                request.Headers.Add("Cookie", cookie);
                var response = await _client.SendAsync(request);
                content += await response.Content.ReadAsStringAsync();
                if (content.Contains("view_authgroup"))
                    break;
            }

            // Assert
            Assert.Contains("add_authgroup", content);
            Assert.Contains("view_authgroup", content);
        }

        private async Task<string> LoginAndGetCookieAsync()
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", "admin"),
                new KeyValuePair<string, string>("password", "admin"),
            });

            var loginResponse = await _client.PostAsync("/admin/login/", formContent);
            foreach (var header in loginResponse.Headers.GetValues("Set-Cookie"))
            {
                if (header.Contains(".NDjango.Admin.Auth"))
                    return header.Split(';')[0];
            }
            return null;
        }
    }
}
