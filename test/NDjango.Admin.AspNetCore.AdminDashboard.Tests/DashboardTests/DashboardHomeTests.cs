using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.DashboardTests
{
    public class DashboardHomeTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public DashboardHomeTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task Dashboard_ContainsAllEntityNamesAsync()
        {
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Categories", html);
            Assert.Contains("Restaurants", html);
            Assert.Contains("Restaurant Profiles", html);
            Assert.Contains("Ingredients", html);
            Assert.Contains("Menu Items", html);
        }

        [Fact]
        public async Task Dashboard_ContainsAddLinksAsync()
        {
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("/admin/Category/add/", html);
            Assert.Contains("/admin/Restaurant/add/", html);
            Assert.Contains("/admin/Ingredient/add/", html);
        }

        [Fact]
        public async Task Dashboard_ContainsChangeLinksAsync()
        {
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("/admin/Category/", html);
            Assert.Contains("/admin/Restaurant/", html);
            Assert.Contains("/admin/Ingredient/", html);
        }

        [Fact]
        public async Task Dashboard_ContainsTitleAsync()
        {
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Test Admin", html);
            Assert.Contains("Site administration", html);
        }
    }
}
