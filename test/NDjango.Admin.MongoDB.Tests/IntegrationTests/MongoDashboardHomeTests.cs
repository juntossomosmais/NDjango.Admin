using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.MongoDB.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.MongoDB.Tests.IntegrationTests
{
    public class MongoDashboardHomeTests : IClassFixture<MongoDashboardFixture>
    {
        private readonly HttpClient _client;

        public MongoDashboardHomeTests(MongoDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task Dashboard_ContainsAllEntityNamesAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Test Categories", html);
            Assert.Contains("Test Restaurants", html);
            Assert.Contains("Test Ingredients", html);
        }

        [Fact]
        public async Task Dashboard_ContainsTitleAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Test Mongo Admin", html);
            Assert.Contains("Site administration", html);
        }

        [Fact]
        public async Task Dashboard_ContainsChangeLinksAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("/admin/TestCategory/", html);
            Assert.Contains("/admin/TestRestaurant/", html);
            Assert.Contains("/admin/TestIngredient/", html);
        }
    }
}
