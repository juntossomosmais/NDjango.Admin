using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.MongoDB.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.MongoDB.Tests.IntegrationTests
{
    public class MongoListViewTests : IClassFixture<MongoDashboardFixture>
    {
        private readonly HttpClient _client;

        public MongoListViewTests(MongoDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task CategoryList_RendersTableWithRecordsAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<table id=\"result_list\">", html);
            Assert.Contains("Italian", html);
            Assert.Contains("Japanese", html);
            Assert.Contains("Mexican", html);
        }

        [Fact]
        public async Task CategoryList_ShowsCorrectTotalCountAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("3 test categories", html);
        }

        [Fact]
        public async Task CategoryList_ContainsColumnHeadersAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Name", html);
        }

        [Fact]
        public async Task CategoryList_ContainsEditLinksAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("/change/", html);
        }

        [Fact]
        public async Task CategoryList_IsEditable_ContainsAddLinkAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("/admin/TestCategory/add/", html);
        }

        [Fact]
        public async Task RestaurantList_RendersRecordsAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestRestaurant/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Bella Roma", html);
            Assert.Contains("Sakura", html);
        }
    }
}
