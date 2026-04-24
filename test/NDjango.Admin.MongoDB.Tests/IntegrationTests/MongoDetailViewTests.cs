using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using MongoDB.Bson;
using NDjango.Admin.MongoDB.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.MongoDB.Tests.IntegrationTests
{
    public class MongoDetailViewTests : IClassFixture<MongoDashboardFixture>
    {
        private readonly HttpClient _client;
        private readonly MongoDashboardFixture _fixture;

        public MongoDetailViewTests(MongoDashboardFixture fixture)
        {
            _fixture = fixture;
            _client = fixture.GetTestHost().GetTestClient();
        }

        [Fact]
        public async Task CategoryDetail_RendersFieldValuesAsync()
        {
            // Arrange
            var categoryId = _fixture.ItalianCategoryId;

            // Act
            var response = await _client.GetAsync($"/admin/TestCategory/{categoryId}/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Italian", html);
            Assert.Contains("Italian cuisine", html);
        }

        [Fact]
        public async Task CategoryDetail_RendersReadonlyFieldsAsync()
        {
            // Arrange
            var categoryId = _fixture.ItalianCategoryId;

            // Act
            var response = await _client.GetAsync($"/admin/TestCategory/{categoryId}/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("readonly-value", html);
        }

        [Fact]
        public async Task CategoryDetail_HasSaveButtonAsync()
        {
            // Arrange
            var categoryId = _fixture.ItalianCategoryId;

            // Act
            var response = await _client.GetAsync($"/admin/TestCategory/{categoryId}/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("_save_action", html);
        }

        [Fact]
        public async Task InvalidId_Returns404Async()
        {
            // Arrange
            var invalidId = ObjectId.GenerateNewId().ToString();

            // Act
            var response = await _client.GetAsync($"/admin/TestCategory/{invalidId}/change/");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
