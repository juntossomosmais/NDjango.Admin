using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.AspNetCore.AdminDashboard.Dispatchers;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.MiddlewareTests
{
    public class ApiDispatcherMutationTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ApiDispatcherMutationTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
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

        // ── ConvertValue culture-invariance (must match FieldValidator) ─

        [Fact]
        public void ConvertValue_DateTimeWithSlashSeparator_ParsesUnderInvariantCultureRegardlessOfThreadCulture()
        {
            // Arrange — under pt-BR thread culture, default TryParse reads "03/12/2025" as dd/MM (3-Dec).
            // Binder must align with FieldValidator (InvariantCulture, MM/dd/yyyy), so the parsed value
            // is March 12 regardless of the host thread culture.
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");
            try
            {
                // Act
                var result = ApiDispatcher.ConvertValue("03/12/2025", DataType.DateTime);

                // Assert
                Assert.IsType<DateTime>(result);
                Assert.Equal(new DateTime(2025, 3, 12), result);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void ConvertValue_DateOnlyWithSlashSeparator_ParsesUnderInvariantCultureRegardlessOfThreadCulture()
        {
            // Arrange
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");
            try
            {
                // Act
                var result = ApiDispatcher.ConvertValue("03/12/2025", DataType.Date, typeof(DateOnly));

                // Assert
                Assert.IsType<DateOnly>(result);
                Assert.Equal(new DateOnly(2025, 3, 12), result);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void ConvertValue_DateTimeOffsetWithSlashSeparator_ParsesUnderInvariantCultureRegardlessOfThreadCulture()
        {
            // Arrange
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");
            try
            {
                // Act
                var result = ApiDispatcher.ConvertValue("03/12/2025", DataType.DateTime, typeof(DateTimeOffset));

                // Assert
                Assert.IsType<DateTimeOffset>(result);
                var dto = (DateTimeOffset)result;
                Assert.Equal(2025, dto.Year);
                Assert.Equal(3, dto.Month);
                Assert.Equal(12, dto.Day);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void ConvertValue_TimeSpanWithColonSeparator_ParsesUnderInvariantCulture()
        {
            // Arrange — TimeSpan parsing also pinned to InvariantCulture for binder/validator parity.
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");
            try
            {
                // Act
                var result = ApiDispatcher.ConvertValue("14:30:00", DataType.Time);

                // Assert
                Assert.IsType<TimeSpan>(result);
                Assert.Equal(new TimeSpan(14, 30, 0), result);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
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

            // Verify record is actually gone (fetching a deleted record returns 404)
            var fetchResponse = await _client.GetAsync($"/admin/Ingredient/{id}/change/");
            Assert.Equal(HttpStatusCode.NotFound, fetchResponse.StatusCode);
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

        // ── Action POST without form content type returns 400 ───────────

        [Fact]
        public async Task ActionPost_WithoutFormContentType_Returns400Async()
        {
            // Arrange — JSON body must be rejected: HandleActionAsync gates on HasFormContentType.
            var jsonContent = new StringContent(
                "{\"action\":\"delete_selected\"}",
                System.Text.Encoding.UTF8,
                "application/json");

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", jsonContent);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ── Validation re-render uses the create form, not the edit form ─

        [Fact]
        public async Task CreatePost_ValidationError_RendersCreateFormNotEditFormAsync()
        {
            // Arrange — empty required Name fails validation and triggers re-render.
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", ""),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/add/", formData);

            // Assert — validation re-render returns 400 by design.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("This field is required.", html);
            // "Add" path renders the add breadcrumb; the Edit form would render "Change".
            Assert.Contains("Add ", html);
            Assert.DoesNotContain("Change ", html);
        }

        // ── Validation re-render on edit uses the edit form, not create ─

        [Fact]
        public async Task UpdatePost_ValidationError_RendersEditFormNotCreateFormAsync()
        {
            // Arrange — seed a record, then submit an edit with an empty required field.
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MutTest_EditRender_" + Guid.NewGuid().ToString("N")[..6]),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResponse = await _client.PostAsync("/admin/Ingredient/add/", createForm);
            var id = ExtractIdFromRedirect(createResponse.Headers.Location.ToString(), "Ingredient");

            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", ""),
            });

            // Act
            var response = await _client.PostAsync($"/admin/Ingredient/{id}/change/", updateForm);

            // Assert — validation re-render returns 400 by design.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("This field is required.", html);
            Assert.Contains("Change ", html);
            Assert.DoesNotContain("Add ", html);
        }

        // ── Custom action with empty selection is blocked by descriptor guard ─

        [Fact]
        public async Task ActionPost_CustomActionEmptySelection_GuardsByDescriptorAsync()
        {
            // Arrange — test_action is registered without AllowEmptySelection = true, so the
            // empty-selection guard must short-circuit before the handler runs. If the guard
            // fires, we redirect to the list without a _msg= query param (handler not invoked).
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "test_action"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Category/action/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.EndsWith("/admin/Category/", location);
            Assert.DoesNotContain("_msg=", location);
        }

        // Mutants left uncovered by design (documented for future Stryker re-runs):
        //
        // - FetchDatasetAsync isLookup "true" → "false" (id=349): hard to kill without asserting
        //   that the lookup JSON excludes non-lookup attributes. The provider decides the shape,
        //   and the current seed doesn't give us a way to distinguish both projections reliably
        //   at the HTTP level.
        //
        // - StripBlankPasswordFields "continue" removals on non-Password / empty-prop / missing-
        //   key branches (id=355/359/361): a mutation that strips an empty nullable string from
        //   props is not observable through the HTTP layer — the persisted value round-trips the
        //   same as "" in form controls, so both produce the same re-render.
        //
        // - Token ternary "Type == JTokenType.String ? Value<string>() : ToString()" (id=362/363/
        //   364): equivalent under the current POST pipeline. Every password-typed attribute that
        //   reaches StripBlankPasswordFields is a JValue whose Value<string>() and ToString() are
        //   byte-identical.
        //
        // - FormToJObject Lookup attr "DataAttr == null" → "!= null" (id=373 / id=374 NoCoverage):
        //   the mutation either NREs on DataAttr.PropName (no DataAttr) or silently drops FK
        //   values. The existing FK-persist test should kill it, but the assertion only inspects
        //   the edit form, and the FK-missing case degrades to an integration error that isn't
        //   exercised consistently enough to kill the boolean flip.
        //
        // - formValue.FirstOrDefault() → First() (id=378): equivalent. FormCollection never
        //   returns an empty StringValues entry for a key that TryGetValue matched.
        //
        // - "p.Value.Type == JTokenType.Null ? null : ToObject<object>()" forced to else
        //   (id=395): equivalent. ToObject<object>() on a JTokenType.Null value also returns null.
        //
        // - ActionDescriptors?.FirstOrDefault(a => a.Name == actionName) mutations (id=483/484/
        //   485): equivalent under the test fixture. Every registered handler has a matching
        //   descriptor with AllowEmptySelection=false, so FirstOrDefault vs First and == vs !=
        //   resolve to the same descriptor instance. Requires fixture changes to differentiate.
        //
        // - Custom action catch-block error message mutations (id=497/498/499 NoCoverage): the
        //   handler delegates registered in the fixture never throw; covering these would
        //   require adding a throwing action, which is out of scope for this test file.
    }
}
