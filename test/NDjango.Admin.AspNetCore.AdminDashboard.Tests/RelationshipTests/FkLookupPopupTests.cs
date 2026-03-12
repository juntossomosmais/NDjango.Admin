using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
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

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("vForeignKeyRawIdAdminField");
            html.Should().Contain("related-lookup");
        }

        [Fact]
        public async Task FkField_LookupUrl_PointsToRelatedEntityWithPopupParamAsync()
        {
            var response = await _client.GetAsync("/admin/Restaurant/add/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("_popup=1");
            html.Should().Contain("_to_field=id");
            html.Should().Contain("/admin/Category/");
        }

        [Fact]
        public async Task PopupListView_RendersSimplifiedLayoutAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().NotContain("id=\"sidebar\"");
            html.Should().NotContain("id=\"header\"");
            html.Should().Contain("class=\"popup\"");
        }

        [Fact]
        public async Task PopupListView_RowsHavePopupSelectLinksAsync()
        {
            var response = await _client.GetAsync("/admin/Category/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("class=\"popup-select\"");
            html.Should().Contain("data-pk=");
        }

        [Fact]
        public async Task PopupListView_WithSearchEnabled_ShowsSearchBoxAsync()
        {
            // Category has SearchFields configured
            var response = await _client.GetAsync("/admin/Category/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("search-box");
        }

        [Fact]
        public async Task PopupListView_WithoutSearchEnabled_HidesSearchBoxAsync()
        {
            // Restaurant does NOT implement IAdminSettings in test fixtures - no search fields
            var response = await _client.GetAsync("/admin/Restaurant/?_popup=1");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().NotContain("search-box");
        }

        [Fact]
        public async Task PopupListView_SearchPreservesPopupParamsAsync()
        {
            // Category popup should have hidden inputs for _popup and _to_field in the search form
            var response = await _client.GetAsync("/admin/Category/?_popup=1&_to_field=id");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("name=\"_popup\" value=\"1\"");
            html.Should().Contain("name=\"_to_field\" value=\"id\"");
        }
    }
}
