using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.EntityListTests
{
    public class ListViewTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ListViewTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task CategoryList_RendersTableWithRecordsAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("<table id=\"result_list\">");
            html.Should().Contain("Italian");
            html.Should().Contain("Japanese");
            html.Should().Contain("Mexican");
        }

        [Fact]
        public async Task CategoryList_ShowsCorrectTotalCountAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("3 categories");
        }

        [Fact]
        public async Task CategoryList_ContainsColumnHeadersAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("Id");
            html.Should().Contain("Category Name");
        }

        [Fact]
        public async Task CategoryList_ContainsEditLinksAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("/change/");
        }

        [Fact]
        public async Task CategoryList_ContainsAddLinkAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("/admin/Category/add/");
            html.Should().Contain("Add category");
        }

        [Fact]
        public async Task CategoryList_ContainsSidebarAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("id=\"sidebar\"");
            html.Should().Contain("sidebar-model-item");
        }

        [Fact]
        public async Task RestaurantList_RendersRecordsAsync()
        {
            var response = await _client.GetAsync("/admin/Restaurant/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Bella Roma");
            html.Should().Contain("Sakura");
        }
    }
}
