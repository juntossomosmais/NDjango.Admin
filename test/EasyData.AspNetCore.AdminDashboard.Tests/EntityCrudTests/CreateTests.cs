using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    public class CreateTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public CreateTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task CreateForm_RendersFieldsAsync()
        {
            var response = await _client.GetAsync("/admin/Category/add/");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Add category");
            html.Should().Contain("Category Name");
            html.Should().Contain("Description");
            html.Should().Contain("<form");
            html.Should().Contain("method=\"post\"");
        }

        [Fact]
        public async Task CreateForm_ContainsSaveButtonsAsync()
        {
            var response = await _client.GetAsync("/admin/Category/add/");
            var html = await response.Content.ReadAsStringAsync();

            html.Should().Contain("Save");
            html.Should().Contain("Save and add another");
            html.Should().Contain("Save and continue editing");
        }

        [Fact]
        public async Task CreatePost_CreatesRecordAndRedirectsAsync()
        {
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "French"),
                new KeyValuePair<string, string>("Description", "French cuisine"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            var response = await _client.PostAsync("/admin/Category/add/", formData);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.ToString().Should().Contain("/admin/Category/");
        }

        [Fact]
        public async Task CreatePost_SaveAndAddAnother_RedirectsToAddAsync()
        {
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Thai"),
                new KeyValuePair<string, string>("Description", "Thai cuisine"),
                new KeyValuePair<string, string>("_save_action", "add_another"),
            });

            var response = await _client.PostAsync("/admin/Category/add/", formData);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location.ToString().Should().Contain("/admin/Category/add/");
        }
    }
}
