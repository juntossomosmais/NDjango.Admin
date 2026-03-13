using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    public class DeleteTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public DeleteTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task DeleteConfirmation_RendersCorrectlyAsync()
        {
            var response = await _client.GetAsync("/admin/Ingredient/1/delete/");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Are you sure", html);
            Assert.Contains("Yes, I", html);
            Assert.Contains("No, take me back", html);
        }

        [Fact]
        public async Task DeletePost_DeletesRecordAndRedirectsAsync()
        {
            // Create a record to delete
            var formData = new FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("Name", "ToDelete"),
                new System.Collections.Generic.KeyValuePair<string, string>("_save_action", "save"),
            });

            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", formData);
            Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

            // Find the new ingredient's ID by listing
            var listResponse = await _client.GetAsync("/admin/Ingredient/");
            var listHtml = await listResponse.Content.ReadAsStringAsync();
            Assert.Contains("ToDelete", listHtml);

            // Delete using a known seeded ingredient (Id=3, "Basil")
            var deleteResponse = await _client.PostAsync("/admin/Ingredient/3/delete/", new FormUrlEncodedContent(System.Array.Empty<System.Collections.Generic.KeyValuePair<string, string>>()));

            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
            Assert.Contains("/admin/Ingredient/", deleteResponse.Headers.Location.ToString());
        }
    }
}
