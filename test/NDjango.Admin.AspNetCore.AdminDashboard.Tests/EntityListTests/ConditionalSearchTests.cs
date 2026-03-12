using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
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

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("search-box");
        }

        [Fact]
        public async Task EntityWithoutSearchFields_HidesSearchBoxAsync()
        {
            // Restaurant does NOT implement IAdminSettings in test fixtures
            var response = await _client.GetAsync("/admin/Restaurant/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().NotContain("search-box");
        }

        [Fact]
        public async Task EntityWithSearchFields_SearchFiltersByConfiguredFieldsAsync()
        {
            // Category has SearchFields = Name, Description
            var response = await _client.GetAsync("/admin/Category/?q=Italian");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Italian");
            html.Should().NotContain("Japanese");
            html.Should().NotContain("Mexican");
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
            allHtml.Should().Contain("Bella Roma");
            withQHtml.Should().Contain("Bella Roma");
        }
    }
}
