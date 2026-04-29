using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.MongoDB.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.MongoDB.Tests.IntegrationTests
{
    public class MongoSortingTests : IClassFixture<MongoDashboardFixture>
    {
        private readonly HttpClient _client;

        public MongoSortingTests(MongoDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task SortByName_Ascending_ReturnsOrderedResultsAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/?sort=Name&dir=asc");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            var italianPos = html.IndexOf("Italian");
            var japanesePos = html.IndexOf("Japanese");
            var mexicanPos = html.IndexOf("Mexican");

            Assert.True(italianPos < japanesePos);
            Assert.True(japanesePos < mexicanPos);
        }

        [Fact]
        public async Task SortByName_Descending_ReturnsReverseOrderAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/?sort=Name&dir=desc");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            var italianPos = html.IndexOf("Italian");
            var mexicanPos = html.IndexOf("Mexican");

            Assert.True(mexicanPos < italianPos);
        }

        [Fact]
        public async Task ColumnHeaders_ContainSortLinksAsync()
        {
            // Arrange
            // Act
            var response = await _client.GetAsync("/admin/TestCategory/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("sort=Name", html);
            Assert.Contains("dir=asc", html);
        }
    }
}
