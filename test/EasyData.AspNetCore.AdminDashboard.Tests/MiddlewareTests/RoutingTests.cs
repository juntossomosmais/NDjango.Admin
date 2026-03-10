using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.MiddlewareTests
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

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task DashboardRoot_Returns200Async()
        {
            var response = await _client.GetAsync("/admin/");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.MediaType.Should().Be("text/html");
        }

        [Theory]
        [InlineData("/admin/Category/")]
        [InlineData("/admin/Restaurant/")]
        [InlineData("/admin/Ingredient/")]
        public async Task EntityList_ReturnsOkAsync(string url)
        {
            var response = await _client.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task EntityAddForm_ReturnsOkAsync()
        {
            var response = await _client.GetAsync("/admin/Category/add/");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task NonExistentEntity_Returns404Async()
        {
            var response = await _client.GetAsync("/admin/DoesNotExist/");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
