using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityListTests
{
    public class ConditionalSearchTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ConditionalSearchTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task EntityWithSearchFields_ShowsSearchBoxAsync()
        {
            // Category implements IAdminSettings<Category> with SearchFields
            var response = await _client.GetAsync("/admin/Category/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("search-box", html);
        }

        [Fact]
        public async Task EntityWithoutSearchFields_HidesSearchBoxAsync()
        {
            // Restaurant does NOT implement IAdminSettings in test fixtures
            var response = await _client.GetAsync("/admin/Restaurant/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("search-box", html);
        }

        [Fact]
        public async Task EntityWithSearchFields_SearchFiltersByConfiguredFieldsAsync()
        {
            // Category has SearchFields = Name, Description
            var response = await _client.GetAsync("/admin/Category/?q=Italian");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Italian", html);
            Assert.DoesNotContain("Japanese", html);
            Assert.DoesNotContain("Mexican", html);
        }

        [Fact]
        public async Task EntityWithoutSearchFields_IgnoresQueryParamAsync()
        {
            // GET without search
            var allResponse = await _client.GetAsync("/admin/Restaurant/");
            var allHtml = await allResponse.Content.ReadAsStringAsync();

            // GET with search param
            var withQResponse = await _client.GetAsync("/admin/Restaurant/?q=Bella");
            var withQHtml = await withQResponse.Content.ReadAsStringAsync();

            // Both should show the same records since Restaurant has no SearchFields
            Assert.Contains("Bella Roma", allHtml);
            Assert.Contains("Bella Roma", withQHtml);
        }
    }
}
