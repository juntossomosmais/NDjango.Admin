using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.DashboardTests
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

            html.Should().Contain("Categories");
            html.Should().Contain("Restaurants");
            html.Should().Contain("Restaurant Profiles");
            html.Should().Contain("Ingredients");
            html.Should().Contain("Menu Items");
        }

        [Fact]
        public async Task Dashboard_ContainsAddLinksAsync()
        {
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("/admin/Category/add/");
            html.Should().Contain("/admin/Restaurant/add/");
            html.Should().Contain("/admin/Ingredient/add/");
        }

        [Fact]
        public async Task Dashboard_ContainsChangeLinksAsync()
        {
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("/admin/Category/");
            html.Should().Contain("/admin/Restaurant/");
            html.Should().Contain("/admin/Ingredient/");
        }

        [Fact]
        public async Task Dashboard_ContainsTitleAsync()
        {
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("Test Admin");
            html.Should().Contain("Site administration");
        }
    }
}
