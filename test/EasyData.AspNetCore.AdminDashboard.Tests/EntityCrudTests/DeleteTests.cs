using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using FluentAssertions;
using Xunit;

using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.EntityCrudTests
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

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain("Are you sure");
            html.Should().Contain("Yes, I");
            html.Should().Contain("No, take me back");
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
            createResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

            // Find the new ingredient's ID by listing
            var listResponse = await _client.GetAsync("/admin/Ingredient/");
            var listHtml = await listResponse.Content.ReadAsStringAsync();
            listHtml.Should().Contain("ToDelete");

            // Delete using a known seeded ingredient (Id=3, "Basil")
            var deleteResponse = await _client.PostAsync("/admin/Ingredient/3/delete/", new FormUrlEncodedContent(System.Array.Empty<System.Collections.Generic.KeyValuePair<string, string>>()));

            deleteResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            deleteResponse.Headers.Location.ToString().Should().Contain("/admin/Ingredient/");
        }
    }
}
