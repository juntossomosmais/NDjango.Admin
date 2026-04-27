using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using MongoDB.Bson;
using MongoDB.Driver;
using NDjango.Admin.MongoDB.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.MongoDB.Tests.IntegrationTests
{
    public class MongoUserEntityCrudTests : IClassFixture<MongoCrudFixture>
    {
        private readonly HttpClient _client;
        private readonly MongoCrudFixture _fixture;

        public MongoUserEntityCrudTests(MongoCrudFixture fixture)
        {
            _fixture = fixture;
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task CreatePost_TestCategory_CreatesDocumentAndRedirectsAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Brazilian"),
                new KeyValuePair<string, string>("Description", "Brazilian cuisine"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/TestCategory/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/TestCategory/", response.Headers.Location.ToString());

            var database = _fixture.GetDatabase();
            var collection = database.GetCollection<TestCategory>("categories");
            var created = await collection.Find(c => c.Name == "Brazilian").FirstOrDefaultAsync();
            Assert.NotNull(created);
            Assert.Equal("Brazilian cuisine", created.Description);
            Assert.NotEqual(ObjectId.Empty, created.Id);
        }

        [Fact]
        public async Task CreatePost_TestCategory_SetsCreatedAtTimestampAsync()
        {
            // Arrange
            var beforeCreate = DateTime.UtcNow.AddSeconds(-1);
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "TimestampTest"),
                new KeyValuePair<string, string>("Description", "Timestamp test"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/TestCategory/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var database = _fixture.GetDatabase();
            var collection = database.GetCollection<TestCategory>("categories");
            var created = await collection.Find(c => c.Name == "TimestampTest").FirstOrDefaultAsync();
            Assert.NotNull(created);
            Assert.True(created.CreatedAt >= beforeCreate, "CreatedAt should be set to a recent UTC time");
            Assert.True(created.UpdatedAt >= beforeCreate, "UpdatedAt should be set on creation");
        }

        [Fact]
        public async Task UpdatePost_TestCategory_UpdatesFieldsAndRedirectsAsync()
        {
            // Arrange
            var database = _fixture.GetDatabase();
            var collection = database.GetCollection<TestCategory>("categories");
            var category = new TestCategory { Name = "ToUpdate", Description = "Original" };
            await collection.InsertOneAsync(category);

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Updated"),
                new KeyValuePair<string, string>("Description", "Updated description"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync($"/admin/TestCategory/{category.Id}/change/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/TestCategory/", response.Headers.Location.ToString());

            var updated = await collection.Find(c => c.Id == category.Id).FirstOrDefaultAsync();
            Assert.NotNull(updated);
            Assert.Equal("Updated", updated.Name);
            Assert.Equal("Updated description", updated.Description);
        }

        [Fact]
        public async Task UpdatePost_TestCategory_UpdatesTimestampButPreservesCreatedAtAsync()
        {
            // Arrange
            var database = _fixture.GetDatabase();
            var collection = database.GetCollection<TestCategory>("categories");
            var originalCreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var category = new TestCategory
            {
                Name = "TimestampPreserve",
                Description = "Original",
                CreatedAt = originalCreatedAt,
                UpdatedAt = originalCreatedAt,
            };
            await collection.InsertOneAsync(category);

            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "TimestampPreserveUpdated"),
                new KeyValuePair<string, string>("Description", "Changed"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync($"/admin/TestCategory/{category.Id}/change/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var updated = await collection.Find(c => c.Id == category.Id).FirstOrDefaultAsync();
            Assert.NotNull(updated);
            Assert.True(updated.UpdatedAt > originalCreatedAt, "UpdatedAt should have been updated to a later time");
            // CreatedAt is not set on update, so it retains the value from the existing document
            Assert.Equal(originalCreatedAt, updated.CreatedAt);
        }

        [Fact]
        public async Task DeletePost_TestCategory_DeletesDocumentAndRedirectsAsync()
        {
            // Arrange
            var database = _fixture.GetDatabase();
            var collection = database.GetCollection<TestCategory>("categories");
            var category = new TestCategory { Name = "ToDelete", Description = "Will be deleted" };
            await collection.InsertOneAsync(category);

            var formData = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>());

            // Act
            var response = await _client.PostAsync($"/admin/TestCategory/{category.Id}/delete/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/TestCategory/", response.Headers.Location.ToString());

            var deleted = await collection.Find(c => c.Id == category.Id).FirstOrDefaultAsync();
            Assert.Null(deleted);
        }

        [Fact]
        public async Task BulkDelete_TestCategory_DeletesSelectedAndLeavesRemainingAsync()
        {
            // Arrange
            var database = _fixture.GetDatabase();
            var collection = database.GetCollection<TestCategory>("categories");
            var cat1 = new TestCategory { Name = "BulkDel1", Description = "Bulk 1" };
            var cat2 = new TestCategory { Name = "BulkDel2", Description = "Bulk 2" };
            var cat3 = new TestCategory { Name = "BulkKeep", Description = "Bulk keep" };
            await collection.InsertManyAsync(new[] { cat1, cat2, cat3 });

            // Post directly to the bulk delete confirmation endpoint
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("_selected_ids", cat1.Id.ToString()),
                new KeyValuePair<string, string>("_selected_ids", cat2.Id.ToString()),
            });

            // Act
            var response = await _client.PostAsync("/admin/TestCategory/action/delete/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var deleted1 = await collection.Find(c => c.Id == cat1.Id).FirstOrDefaultAsync();
            var deleted2 = await collection.Find(c => c.Id == cat2.Id).FirstOrDefaultAsync();
            var remaining = await collection.Find(c => c.Id == cat3.Id).FirstOrDefaultAsync();
            Assert.Null(deleted1);
            Assert.Null(deleted2);
            Assert.NotNull(remaining);
            Assert.Equal("BulkKeep", remaining.Name);
        }

        [Fact]
        public async Task ReadOnlyCollection_TestIngredient_HasNoAddButtonAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/TestIngredient/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("Add test ingredient", html, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ReadOnlyCollection_TestIngredient_DetailHasNoSaveButtonAsync()
        {
            // Arrange
            var database = _fixture.GetDatabase();
            var collection = database.GetCollection<TestIngredient>("ingredients");
            var ingredient = await collection.Find(_ => true).FirstOrDefaultAsync();
            Assert.NotNull(ingredient);

            // Act
            var response = await _client.GetAsync($"/admin/TestIngredient/{ingredient.Id}/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("_save_action", html);
            Assert.DoesNotContain(">Save<", html);
        }

        [Fact]
        public async Task EditableCollection_TestCategory_AddFormRendersEditableFieldsAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/TestCategory/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // Name and Description should be editable fields (input or textarea)
            Assert.Contains("Name", html);
            Assert.Contains("Description", html);
            // Save button should be present
            Assert.Contains("_save_action", html);
        }

        [Fact]
        public async Task EditableCollection_TestCategory_ChangeFormRendersEditableFieldsAsync()
        {
            // Arrange
            var categoryId = _fixture.SeededCategoryId;

            // Act
            var response = await _client.GetAsync($"/admin/TestCategory/{categoryId}/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Italian", html);
            // Save button should be present for editable entities
            Assert.Contains("_save_action", html);
        }

        [Fact]
        public async Task EditableCollection_TestCategory_ListShowsAddButtonAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/TestCategory/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("/admin/TestCategory/add/", html);
        }

        [Fact]
        public async Task CreatePost_TestRestaurant_CreatesAndVerifiesInListAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "TestRestaurantCrud"),
                new KeyValuePair<string, string>("Address", "999 Test Ave"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            await _client.PostAsync("/admin/TestRestaurant/add/", formData);
            var listResponse = await _client.GetAsync("/admin/TestRestaurant/");
            var html = await listResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            Assert.Contains("TestRestaurantCrud", html);
            Assert.Contains("999 Test Ave", html);
        }

        [Fact]
        public async Task CreatePost_TestDocumentWithCreatedDate_SetsCreatedDateTimestampAsync()
        {
            // Arrange
            var beforeCreate = DateTime.UtcNow.AddSeconds(-1);
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "CreatedDateTest"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/TestDocumentWithCreatedDate/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var database = _fixture.GetDatabase();
            var collection = database.GetCollection<TestDocumentWithCreatedDate>("documents_with_created_date");
            var created = await collection.Find(c => c.Name == "CreatedDateTest").FirstOrDefaultAsync();
            Assert.NotNull(created);
            Assert.True(created.CreatedDate >= beforeCreate, "CreatedDate should be set to a recent UTC time");
            Assert.True(created.UpdatedDate >= beforeCreate, "UpdatedDate should be set on creation");
        }
    }
}
