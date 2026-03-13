using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.RelationshipTests
{
    public class FkLookupPopupTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public FkLookupPopupTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task FkField_RendersTextInputAndLookupIconAsync()
        {
            var response = await _client.GetAsync("/admin/Restaurant/add/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("vForeignKeyRawIdAdminField", html);
            Assert.Contains("related-lookup", html);
        }

        [Fact]
        public async Task FkField_LookupUrl_PointsToRelatedEntityWithPopupParamAsync()
        {
            var response = await _client.GetAsync("/admin/Restaurant/add/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("_popup=1", html);
            Assert.Contains("_to_field=id", html);
            Assert.Contains("/admin/Category/", html);
        }

        [Fact]
        public async Task PopupListView_RendersSimplifiedLayoutAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("id=\"sidebar\"", html);
            Assert.DoesNotContain("id=\"header\"", html);
            Assert.Contains("class=\"popup\"", html);
        }

        [Fact]
        public async Task PopupListView_RowsHavePopupSelectLinksAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("class=\"popup-select\"", html);
            Assert.Contains("data-pk=", html);
        }

        [Fact]
        public async Task PopupListView_WithSearchEnabled_ShowsSearchBoxAsync()
        {
            // Category has SearchFields configured
            var response = await _client.GetAsync("/admin/Category/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("search-box", html);
        }

        [Fact]
        public async Task PopupListView_WithoutSearchEnabled_HidesSearchBoxAsync()
        {
            // Restaurant does NOT implement IAdminSettings in test fixtures - no search fields
            var response = await _client.GetAsync("/admin/Restaurant/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("search-box", html);
        }

        [Fact]
        public async Task PopupListView_SearchPreservesPopupParamsAsync()
        {
            // Category popup should have hidden inputs for _popup and _to_field in the search form
            var response = await _client.GetAsync("/admin/Category/?_popup=1&_to_field=id");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("name=\"_popup\" value=\"1\"", html);
            Assert.Contains("name=\"_to_field\" value=\"id\"", html);
        }
    }
}
