using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityListTests
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

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<table id=\"result_list\">", html);
            Assert.Contains("Italian", html);
            Assert.Contains("Japanese", html);
            Assert.Contains("Mexican", html);
        }

        [Fact]
        public async Task CategoryList_ShowsCorrectTotalCountAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("3 categories", html);
        }

        [Fact]
        public async Task CategoryList_ContainsColumnHeadersAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Id", html);
            Assert.Contains("Category Name", html);
        }

        [Fact]
        public async Task CategoryList_ContainsEditLinksAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("/change/", html);
        }

        [Fact]
        public async Task CategoryList_ContainsAddLinkAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("/admin/Category/add/", html);
            Assert.Contains("Add category", html);
        }

        [Fact]
        public async Task CategoryList_ContainsSidebarAsync()
        {
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("id=\"sidebar\"", html);
            Assert.Contains("sidebar-model-item", html);
        }

        [Fact]
        public async Task RestaurantList_RendersRecordsAsync()
        {
            var response = await _client.GetAsync("/admin/Restaurant/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Bella Roma", html);
            Assert.Contains("Sakura", html);
        }
    }
}
