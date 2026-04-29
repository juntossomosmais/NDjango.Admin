using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.TestHost;
using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;
using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    public class CompositeKeyCrudTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public CompositeKeyCrudTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task List_CompositeKeyEntity_RendersListPageAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/MenuItemIngredient/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Menu Item Ingredient", html);
        }

        [Fact]
        public async Task List_CompositeKeyEntity_RendersRowsWithCompositeKeyLinksAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/MenuItemIngredient/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // Links should contain comma-separated composite keys
            Assert.Contains("/change/", html);
        }

        [Fact]
        public async Task CreateForm_CompositeKeyEntity_RendersLookupFieldsAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/MenuItemIngredient/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Add menu item ingredient", html);
            // Should have lookup fields for MenuItem and Ingredient
            Assert.Contains("Menu Item", html);
            Assert.Contains("Ingredient", html);
        }

        [Fact]
        public async Task CreateForm_CompositeKeyEntity_FkLookupFields_AreEditableAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/MenuItemIngredient/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert - add form must NOT render FK lookup fields as read-only
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("readonly-value", html);
            // FK lookup fields should render as editable text inputs with lookup icons
            Assert.Contains("vForeignKeyRawIdAdminField", html);
            Assert.Contains("related-lookup", html);
        }

        [Fact]
        public async Task CreatePost_CompositeKeyEntity_CreatesRecordAndRedirectsAsync()
        {
            // Arrange
            // Use IDs that exist: MenuItem.Id=1 and Ingredient.Id=3 (Basil, not yet linked)
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("MenuItemId", "2"),
                new KeyValuePair<string, string>("IngredientId", "1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/MenuItemIngredient/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/MenuItemIngredient/", response.Headers.Location.ToString());
        }

        [Fact]
        public async Task CreatePost_CompositeKeyEntity_SaveAndContinue_RedirectsWithCompositeKeyAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("MenuItemId", "2"),
                new KeyValuePair<string, string>("IngredientId", "2"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });

            // Act
            var response = await _client.PostAsync("/admin/MenuItemIngredient/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            // The redirect URL should contain the composite key as "2,2"
            var location = response.Headers.Location.ToString();
            Assert.Contains("/admin/MenuItemIngredient/2,2/change/", location);
        }

        [Fact]
        public async Task EditForm_CompositeKeyEntity_RendersPrefilled_ForExistingRecordAsync()
        {
            // Arrange - use seeded data: MenuItem 1, Ingredient 1
            // Act
            var response = await _client.GetAsync("/admin/MenuItemIngredient/1,1/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Change menu item ingredient", html);
        }

        [Fact]
        public async Task EditForm_CompositeKeyEntity_PkLookupFields_AreReadOnlyAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/MenuItemIngredient/1,1/change/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // FK lookup fields that form the composite key should be rendered as read-only
            Assert.Contains("readonly-value", html);
        }

        [Fact]
        public async Task DeleteForm_CompositeKeyEntity_RendersConfirmationAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/MenuItemIngredient/1,2/delete/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Are you sure", html);
        }

        [Fact]
        public async Task DeletePost_CompositeKeyEntity_DeletesAndRedirectsAsync()
        {
            // Arrange - first create a record to delete
            var createFormData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("MenuItemId", "1"),
                new KeyValuePair<string, string>("IngredientId", "3"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            await _client.PostAsync("/admin/MenuItemIngredient/add/", createFormData);

            // Act - delete it
            var response = await _client.PostAsync("/admin/MenuItemIngredient/1,3/delete/", new FormUrlEncodedContent(new KeyValuePair<string, string>[0]));

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/MenuItemIngredient/", response.Headers.Location.ToString());
        }

        [Fact]
        public async Task BulkDelete_CompositeKeyEntity_CreatesAndDeletesTwoRecordsAsync()
        {
            // Arrange - create 2 junction records using ingredient IDs 4 and 5 (exclusive to this test)
            var createFormData1 = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("MenuItemId", "1"),
                new KeyValuePair<string, string>("IngredientId", "4"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            await _client.PostAsync("/admin/MenuItemIngredient/add/", createFormData1);

            var createFormData2 = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("MenuItemId", "1"),
                new KeyValuePair<string, string>("IngredientId", "5"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });
            await _client.PostAsync("/admin/MenuItemIngredient/add/", createFormData2);

            // Act - post action with delete_selected and _selected_ids containing encoded composite keys
            var actionFormData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("action", "delete_selected"),
                new KeyValuePair<string, string>("_selected_ids", "1,4"),
                new KeyValuePair<string, string>("_selected_ids", "1,5"),
            });
            var actionResponse = await _client.PostAsync("/admin/MenuItemIngredient/action/", actionFormData);

            // Assert - should redirect to bulk-delete confirmation page
            Assert.Equal(HttpStatusCode.Redirect, actionResponse.StatusCode);
            var confirmUrl = actionResponse.Headers.Location.ToString();
            Assert.Contains("/admin/MenuItemIngredient/action/delete/", confirmUrl);
            Assert.Contains("ids=", confirmUrl);

            // Act - GET the bulk-delete confirmation page
            var confirmGetResponse = await _client.GetAsync(confirmUrl);
            var confirmHtml = await confirmGetResponse.Content.ReadAsStringAsync();

            // Assert - confirmation page renders
            Assert.Equal(HttpStatusCode.OK, confirmGetResponse.StatusCode);
            Assert.Contains("Are you sure", confirmHtml);

            // Act - POST to confirm bulk delete
            var bulkDeleteFormData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("_selected_ids", "1,4"),
                new KeyValuePair<string, string>("_selected_ids", "1,5"),
            });
            var deleteResponse = await _client.PostAsync("/admin/MenuItemIngredient/action/delete/", bulkDeleteFormData);

            // Assert - should redirect to list page with success message
            Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
            var deleteLocation = deleteResponse.Headers.Location.ToString();
            Assert.Contains("/admin/MenuItemIngredient/", deleteLocation);
            Assert.Contains("_msg=", deleteLocation);
            Assert.Contains("success", deleteLocation);

            // Verify the records are deleted by checking list page no longer has them
            var listResponse = await _client.GetAsync("/admin/MenuItemIngredient/");
            var listHtml = await listResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            // The deleted composite keys should not appear as links
            Assert.DoesNotContain("/admin/MenuItemIngredient/1,4/change/", listHtml);
            Assert.DoesNotContain("/admin/MenuItemIngredient/1,5/change/", listHtml);
        }

        [Fact]
        public async Task EditForm_CompositeKeyEntity_InvalidEncodedKey_Returns400Async()
        {
            // Arrange & Act - "INVALID" cannot be decoded as a 2-part composite key
            var response = await _client.GetAsync("/admin/MenuItemIngredient/INVALID/change/");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task EditForm_CompositeKeyEntity_SingleValueForCompositeKey_Returns400Async()
        {
            // Arrange & Act - composite key expects 2 parts but only 1 provided
            var response = await _client.GetAsync("/admin/MenuItemIngredient/1/change/");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task DeleteForm_CompositeKeyEntity_InvalidEncodedKey_Returns400Async()
        {
            // Arrange & Act - "INVALID" cannot be decoded as a 2-part composite key
            var response = await _client.GetAsync("/admin/MenuItemIngredient/INVALID/delete/");

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task DeletePost_CompositeKeyEntity_InvalidEncodedKey_Returns400Async()
        {
            // Arrange & Act - POST delete with malformed composite key
            var response = await _client.PostAsync(
                "/admin/MenuItemIngredient/INVALID/delete/",
                new FormUrlEncodedContent(new KeyValuePair<string, string>[0]));

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdatePost_CompositeKeyEntity_InvalidEncodedKey_Returns400Async()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("MenuItemId", "1"),
                new KeyValuePair<string, string>("IngredientId", "1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act - POST update with malformed composite key
            var response = await _client.PostAsync("/admin/MenuItemIngredient/INVALID/change/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
