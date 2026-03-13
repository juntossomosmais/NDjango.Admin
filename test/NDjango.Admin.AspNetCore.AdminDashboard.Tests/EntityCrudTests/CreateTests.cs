using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
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

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Add category", html);
            Assert.Contains("Category Name", html);
            Assert.Contains("Description", html);
            Assert.Contains("<form", html);
            Assert.Contains("method=\"post\"", html);
        }

        [Fact]
        public async Task CreateForm_ContainsSaveButtonsAsync()
        {
            var response = await _client.GetAsync("/admin/Category/add/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Save", html);
            Assert.Contains("Save and add another", html);
            Assert.Contains("Save and continue editing", html);
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

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/Category/", response.Headers.Location.ToString());
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

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/Category/add/", response.Headers.Location.ToString());
        }
    }
}
