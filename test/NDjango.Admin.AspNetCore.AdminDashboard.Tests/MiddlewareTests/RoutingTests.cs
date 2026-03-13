using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.MiddlewareTests
{
    public class RoutingTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public RoutingTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task UnmatchedRoute_Returns404Async()
        {
            var response = await _client.GetAsync("/admin/nonexistent/bad/route/");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DashboardRoot_Returns200Async()
        {
            var response = await _client.GetAsync("/admin/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType.MediaType);
        }

        [Theory]
        [InlineData("/admin/Category/")]
        [InlineData("/admin/Restaurant/")]
        [InlineData("/admin/Ingredient/")]
        public async Task EntityList_ReturnsOkAsync(string url)
        {
            var response = await _client.GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task EntityAddForm_ReturnsOkAsync()
        {
            var response = await _client.GetAsync("/admin/Category/add/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task NonExistentEntity_Returns404Async()
        {
            var response = await _client.GetAsync("/admin/DoesNotExist/");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
