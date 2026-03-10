using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.RelationshipTests
{
    public class ForeignKeyTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ForeignKeyTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task RestaurantAddForm_RendersCategory_DropdownAsync()
        {
            var response = await _client.GetAsync("/admin/Restaurant/add/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("<select");
            html.Should().Contain("Italian");
            html.Should().Contain("Japanese");
            html.Should().Contain("Mexican");
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

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        }

        [Fact]
        public async Task MenuItemAddForm_RendersRestaurant_DropdownAsync()
        {
            var response = await _client.GetAsync("/admin/MenuItem/add/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("<select");
            html.Should().Contain("Bella Roma");
            html.Should().Contain("Sakura");
        }

        [Fact]
        public async Task FkDropdown_UsesDataAttributePropName_NotNavigationNameAsync()
        {
            // The FK dropdown should use name="CategoryId" (the data attribute prop name),
            // not name="Category" (the navigation property name).
            // Before the fix, the form sent the navigation name which didn't match
            // what FormToJObject expected, causing FK constraint violations.
            var response = await _client.GetAsync("/admin/Restaurant/add/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("name=\"CategoryId\"",
                "FK dropdown should use the FK property name, not the navigation property name");
            html.Should().NotContain("name=\"Category\"",
                "FK dropdown should not use the navigation property name");
        }

        [Fact]
        public async Task MenuItemFkDropdown_UsesRestaurantId_NotRestaurantAsync()
        {
            var response = await _client.GetAsync("/admin/MenuItem/add/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("name=\"RestaurantId\"",
                "FK dropdown should use the FK property name");
        }

        [Fact]
        public async Task EditFormFkDropdown_UsesDataAttributePropNameAsync()
        {
            // The FK dropdown on edit forms should also use the data attribute prop name
            var response = await _client.GetAsync("/admin/Restaurant/1/change/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("name=\"CategoryId\"");
        }
    }
}
