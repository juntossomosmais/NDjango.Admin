using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.MongoDB.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.MongoDB.Tests.IntegrationTests
{
    public class MongoSearchTests : IClassFixture<MongoDashboardFixture>
    {
        private readonly HttpClient _client;

        public MongoSearchTests(MongoDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task Search_FiltersResultsAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/?q=Italian");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("Italian", html);
            Assert.DoesNotContain("Japanese", html);
            Assert.DoesNotContain("Mexican", html);
        }

        [Fact]
        public async Task Search_ShowsFilteredCountAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/?q=Italian");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("1 test category", html);
        }

        [Fact]
        public async Task Search_NoMatch_ShowsZeroResultsAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/?q=NonExistentValue");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("0 test categories", html);
        }
    }
}
