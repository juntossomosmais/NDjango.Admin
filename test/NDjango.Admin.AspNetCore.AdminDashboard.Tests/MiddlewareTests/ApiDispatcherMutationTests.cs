using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Dispatchers;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.MiddlewareTests
{
    public class ApiDispatcherMutationTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ApiDispatcherMutationTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        private static string ExtractIdFromRedirect(string locationHeader, string entity)
        {
            var match = Regex.Match(locationHeader, $@"/admin/{entity}/(\d+)/change/");
            Assert.True(match.Success, $"Expected redirect to /admin/{entity}/{{id}}/change/ but got: {locationHeader}");
            return match.Groups[1].Value;
        }

        // ── ConvertValue unit tests ──────────────────────────────────────

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ConvertValue_EmptyOrNullString_ReturnsOriginalValueAsync(string input)
        {
            // Arrange
            // input is empty or null

            // Act
            var result = ApiDispatcher.ConvertValue(input, DataType.Int32);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void ConvertValue_NullableDateOnly_ParsesAsDateOnly()
        {
            // Arrange
            var input = "2024-01-15";

            // Act
            var result = ApiDispatcher.ConvertValue(input, DataType.Date, typeof(DateOnly?));

            // Assert
            Assert.IsType<DateOnly>(result);
            Assert.Equal(new DateOnly(2024, 1, 15), result);
        }

        [Fact]
        public void ConvertValue_TimeOnlyType_ParsesAsTimeOnly()
        {
            // Arrange
            var input = "14:30:00";

            // Act
            var result = ApiDispatcher.ConvertValue(input, DataType.Time, typeof(TimeOnly));

            // Assert
            Assert.IsType<TimeOnly>(result);
            Assert.Equal(new TimeOnly(14, 30, 0), result);
        }

        [Fact]
        public void ConvertValue_BoolOn_ReturnsTrue()
        {
            // Arrange
            var input = "on";

            // Act
            var result = ApiDispatcher.ConvertValue(input, DataType.Bool);

            // Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public void ConvertValue_BoolTrueLowercase_ReturnsTrue()
        {
            // Arrange
            var input = "true";

            // Act
            var result = ApiDispatcher.ConvertValue(input, DataType.Bool);

            // Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public void ConvertValue_BoolTrueUppercase_ReturnsTrue()
        {
            // Arrange
            var input = "True";

            // Act
            var result = ApiDispatcher.ConvertValue(input, DataType.Bool);

            // Assert
            Assert.Equal(true, result);
        }

        [Fact]
        public void ConvertValue_BoolNonMatch_ReturnsFalse()
        {
            // Arrange
            var input = "false";

            // Act
            var result = ApiDispatcher.ConvertValue(input, DataType.Bool);

            // Assert
            Assert.Equal(false, result);
        }

        // ── Create: missing _save_action defaults to "save" ─────────────

        [Fact]
        public async Task CreatePost_WithoutSaveAction_RedirectsToEntityListAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_NoSave_" + Guid.NewGuid().ToString("N")[..6]),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.EndsWith("/admin/Category/", location);
        }

        // ── Update: missing _save_action defaults to "save" ─────────────

        [Fact]
        public async Task UpdatePost_WithoutSaveAction_RedirectsToEntityListAsync()
        {
            // Arrange — create a record first
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_UpdNoSave_" + Guid.NewGuid().ToString("N")[..6]),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", createForm);
            var id = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient");

            // Update without _save_action
            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_UpdNoSave_Modified"),
            });

            // Act
            var response = await _client.PostAsync($"/admin/Ingredient/{id}/change/", updateForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.EndsWith("/admin/Ingredient/", location);
        }

        // ── Update: _save_action=add_another redirects to add form ──────

        [Fact]
        public async Task UpdatePost_SaveActionAddAnother_RedirectsToAddFormAsync()
        {
            // Arrange — create a record first
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_AddAnother_" + Guid.NewGuid().ToString("N")[..6]),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", createForm);
            var id = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient");

            // Update with add_another
            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_AddAnother_Modified"),
                new KeyValuePair<string, string>("_save_action", "add_another"),
            });

            // Act
            var response = await _client.PostAsync($"/admin/Ingredient/{id}/change/", updateForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("/admin/Ingredient/add/", location);
        }

        // ── Create: FK field is persisted via FormToJObject lookup path ──

        [Fact]
        public async Task CreatePost_WithForeignKey_PersistsFkValueAsync()
        {
            // Arrange
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_FK_" + Guid.NewGuid().ToString("N")[..6]),
                new KeyValuePair<string, string>("Address", "123 Mutation St"),
                new KeyValuePair<string, string>("CategoryId", "1"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Restaurant/add/", createForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            Assert.Contains("value=\"1\"", editHtml);
        }

        // ── Action: missing action field redirects to list ──────────────

        [Fact]
        public async Task ActionPost_WithoutActionField_RedirectsToEntityListAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("_selected_ids", "1"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.EndsWith("/admin/Category/", location);
        }

        // ── Action: delete_selected redirect contains "ids=" in query ───

        [Fact]
        public async Task ActionPost_DeleteSelected_RedirectContainsIdsQueryParamAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "delete_selected"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
                new KeyValuePair<string, string>("_selected_ids", "2"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("ids=", location);
        }

        // ── Bulk delete: records actually deleted + singular message ─────

        [Fact]
        public async Task BulkDeletePost_SingleRecord_DeletesAndShowsSingularMessageAsync()
        {
            // Arrange — create a record to delete
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_BulkSingle_" + Guid.NewGuid().ToString("N")[..6]),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", createForm);
            var id = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient");

            var deleteForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("_selected_ids", id),
            });

            // Act
            var response = await _client.PostAsync("/admin/Ingredient/action/delete/", deleteForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            var decodedLocation = Uri.UnescapeDataString(location).Replace(" ", "+").ToLower();
            Assert.Contains("deleted+1+ingredient.", decodedLocation);

            // Verify record is actually gone (fetching a deleted record throws)
            await Assert.ThrowsAnyAsync<Exception>(
                () => _client.GetAsync($"/admin/Ingredient/{id}/change/"));
        }

        [Fact]
        public async Task BulkDeletePost_MultipleRecords_DeletesAndShowsPluralMessageAsync()
        {
            // Arrange — create two records to delete
            var ids = new List<string>();
            for (var i = 0; i < 2; i++)
            {
                var createForm = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Name", $"MutTest_BulkPlural_{i}_" + Guid.NewGuid().ToString("N")[..6]),
                    new KeyValuePair<string, string>("_save_action", "continue"),
                });
                var createResponse = await _client.PostAsync("/admin/Ingredient/add/", createForm);
                ids.Add(ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient"));
            }

            var deleteForm = new FormUrlEncodedContent(
                ids.Select(id => new KeyValuePair<string, string>("_selected_ids", id))
            );

            // Act
            var response = await _client.PostAsync("/admin/Ingredient/action/delete/", deleteForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("deleted+2+ingredients", Uri.UnescapeDataString(location).Replace(" ", "+").ToLower());
        }

        // ── Lookup: returns JSON with application/json content type ──────

        [Fact]
        public async Task LookupGet_ReturnsJsonResponseAsync()
        {
            // Arrange
            // Category has seeded data

            // Act
            var response = await _client.GetAsync("/admin/api/Category/lookup/");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Italian", body);
        }

        // ── Action: empty action name still redirects to entity list ────

        [Fact]
        public async Task ActionPost_EmptyActionName_RedirectsToEntityListAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", ""),
                new KeyValuePair<string, string>("_selected_ids", "1"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.EndsWith("/admin/Category/", location);
        }

        // ── Action: delete_selected with no IDs redirects to list ───────

        [Fact]
        public async Task ActionPost_DeleteSelectedNoIds_RedirectsToEntityListAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "delete_selected"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.EndsWith("/admin/Category/", location);
        }

        // ── Action: unknown action redirects to entity list ─────────────

        [Fact]
        public async Task ActionPost_UnknownAction_RedirectsToEntityListAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "nonexistent_action"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.EndsWith("/admin/Category/", location);
        }

        // ── Delete: entity not found returns 404 ────────────────────────

        [Fact]
        public async Task DeletePost_UnknownEntity_Returns404Async()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>());

            // Act
            var response = await _client.PostAsync("/admin/NonExistentEntity/1/delete/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── Create: entity not found returns 404 ────────────────────────

        [Fact]
        public async Task CreatePost_UnknownEntity_Returns404Async()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Test"),
            });

            // Act
            var response = await _client.PostAsync("/admin/NonExistentEntity/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── Update: entity not found returns 404 ────────────────────────

        [Fact]
        public async Task UpdatePost_UnknownEntity_Returns404Async()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Test"),
            });

            // Act
            var response = await _client.PostAsync("/admin/NonExistentEntity/1/change/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── Action: entity not found returns 404 ────────────────────────

        [Fact]
        public async Task ActionPost_UnknownEntity_Returns404Async()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "delete_selected"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
            });

            // Act
            var response = await _client.PostAsync("/admin/NonExistentEntity/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── Bulk delete: entity not found returns 404 ───────────────────

        [Fact]
        public async Task BulkDeletePost_UnknownEntity_Returns404Async()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("_selected_ids", "1"),
            });

            // Act
            var response = await _client.PostAsync("/admin/NonExistentEntity/action/delete/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── Custom action: successful execution includes message ────────

        [Fact]
        public async Task ActionPost_CustomAction_RedirectsWithSuccessLevelAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "test_action"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("_msg_level=success", location);
            Assert.Contains("_msg=", location);
        }

        // ── Bool field: missing checkbox defaults to false ────────────

        [Fact]
        public async Task CreatePost_MissingBoolField_DefaultsToFalseAsync()
        {
            // Arrange — create Ingredient WITHOUT IsAllergen field
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_BoolDefault_" + Guid.NewGuid().ToString("N")[..6]),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Ingredient/add/", createForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            // IsAllergen checkbox should NOT be checked (value false)
            Assert.DoesNotContain("checked", editHtml.ToLower().Substring(
                editHtml.IndexOf("IsAllergen", StringComparison.OrdinalIgnoreCase)));
        }

        // ── Delete_selected: redirect URL has proper ids= params ────────

        [Fact]
        public async Task ActionPost_DeleteSelected_RedirectUrlContainsAmpersandSeparatedIdsAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "delete_selected"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
                new KeyValuePair<string, string>("_selected_ids", "2"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            // Verify ids are joined with & separator (not concatenated)
            Assert.Contains("ids=1&ids=2", location);
        }

        // ── Lookup: isLookup=true affects dataset scope ─────────────────

        [Fact]
        public async Task LookupGet_ReturnsMultipleRecordsAsJsonArrayAsync()
        {
            // Arrange
            // Category has 3+ seeded records

            // Act
            var response = await _client.GetAsync("/admin/api/Category/lookup/");
            var body = await response.Content.ReadAsStringAsync();

            // Assert — verify all seeded categories are present in lookup
            Assert.Contains("Italian", body);
            Assert.Contains("Japanese", body);
            Assert.Contains("Mexican", body);
        }

        // ── Custom action: error execution includes error level ─────────

        [Fact]
        public async Task ActionPost_ErrorAction_RedirectsWithErrorLevelAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "test_error_action"),
                new KeyValuePair<string, string>("_selected_ids", "1"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Contains("_msg_level=error", location);
        }
    }
}
