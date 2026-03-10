using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.MiddlewareTests
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

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.MediaType.Should().Be("text/css");
        }

        [Fact]
        public async Task JsResource_ReturnsCorrectContentTypeAsync()
        {
            var response = await _client.GetAsync("/admin/js/admin-dashboard.js");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType.MediaType.Should().Be("application/javascript");
        }

        [Fact]
        public async Task NonExistentResource_Returns404Async()
        {
            var response = await _client.GetAsync("/admin/css/nonexistent.css");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
