using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.RelationshipTests
{
    public class ForeignKeyTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ForeignKeyTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task RestaurantAddForm_RendersCategory_LookupFieldAsync()
        {
            var response = await _client.GetAsync("/admin/Restaurant/add/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<input type=\"text\"", html);
            Assert.Contains("class=\"vForeignKeyRawIdAdminField\"", html);
            Assert.Contains("class=\"related-lookup\"", html);
        }

        [Fact]
        public async Task CreateWithFk_CreatesLinkedRecordAsync()
        {
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Test Restaurant"),
                new KeyValuePair<string, string>("Address", "789 Test Blvd"),
                new KeyValuePair<string, string>("CategoryId", "1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            var response = await _client.PostAsync("/admin/Restaurant/add/", formData);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task MenuItemAddForm_RendersRestaurant_LookupFieldAsync()
        {
            var response = await _client.GetAsync("/admin/MenuItem/add/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<input type=\"text\"", html);
            Assert.Contains("class=\"vForeignKeyRawIdAdminField\"", html);
            Assert.Contains("class=\"related-lookup\"", html);
        }

        [Fact]
        public async Task FkDropdown_UsesDataAttributePropName_NotNavigationNameAsync()
        {
            // The FK field should use name="CategoryId" (the data attribute prop name),
            // not name="Category" (the navigation property name).
            var response = await _client.GetAsync("/admin/Restaurant/add/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("name=\"CategoryId\"", html);
            Assert.DoesNotContain("name=\"Category\"", html);
        }

        [Fact]
        public async Task MenuItemFkDropdown_UsesRestaurantId_NotRestaurantAsync()
        {
            var response = await _client.GetAsync("/admin/MenuItem/add/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("name=\"RestaurantId\"", html);
        }

        [Fact]
        public async Task EditFormFkDropdown_UsesDataAttributePropNameAsync()
        {
            // The FK field on edit forms should also use the data attribute prop name
            var response = await _client.GetAsync("/admin/Restaurant/1/change/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("name=\"CategoryId\"", html);
        }
    }
}
