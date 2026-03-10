using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    public class SamlLoginTests : IClassFixture<SamlEnabledFixture>
    {
        private readonly HttpClient _client;

        public SamlLoginTests(SamlEnabledFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task GetLogin_SamlEnabled_ShowsSsoLinkAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/login/");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("alternative-login-section", content);
            Assert.Contains("saml/init/", content);
            Assert.Contains("single sign-on", content.ToLower());
        }

        [Fact]
        public async Task GetSamlInit_SamlEnabled_RedirectsToIdpAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/saml/init/");

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains(SamlEnabledFixture.TestIdpSsoUrl, location);
        }

        [Fact]
        public async Task PostSamlCallback_InvalidResponse_Returns401Async()
        {
            // Arrange
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("SAMLResponse", "not-valid-base64-saml-data"),
            });

            // Act
            var response = await _client.PostAsync("/api/security/saml/callback", formContent);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task PostSamlCallback_EmptyResponse_Returns401Async()
        {
            // Arrange
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("SAMLResponse", ""),
            });

            // Act
            var response = await _client.PostAsync("/api/security/saml/callback", formContent);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetSamlCallback_WrongMethod_Returns405Async()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/api/security/saml/callback");

            // Assert
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        [Fact]
        public async Task GetLogin_SamlEnabled_PasswordLoginStillWorksAsync()
        {
            // Arrange
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", "admin"),
                new KeyValuePair<string, string>("password", "admin"),
            });

            // Act
            var response = await _client.PostAsync("/admin/login/", formContent);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/admin/", response.Headers.Location.ToString());
        }
    }

    public class SamlDisabledLoginTests : IClassFixture<AuthEnabledFixture>
    {
        private readonly HttpClient _client;

        public SamlDisabledLoginTests(AuthEnabledFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task GetLogin_SamlDisabled_DoesNotShowSsoLinkAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/login/");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("alternative-login-section", content);
            Assert.DoesNotContain("saml/init/", content);
        }
    }
}
