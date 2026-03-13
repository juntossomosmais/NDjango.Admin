using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.MiddlewareTests
{
    public class StaticResourceTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public StaticResourceTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task CssResource_ReturnsCorrectContentTypeAsync()
        {
            var response = await _client.GetAsync("/admin/css/admin-dashboard.css");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/css", response.Content.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task JsResource_ReturnsCorrectContentTypeAsync()
        {
            var response = await _client.GetAsync("/admin/js/admin-dashboard.js");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/javascript", response.Content.Headers.ContentType.MediaType);
        }

        [Fact]
        public async Task NonExistentResource_Returns404Async()
        {
            var response = await _client.GetAsync("/admin/css/nonexistent.css");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
