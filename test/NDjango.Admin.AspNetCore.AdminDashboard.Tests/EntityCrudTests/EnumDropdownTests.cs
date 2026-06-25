using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    // Enum fields must render as a <select> dropdown on the add/edit form, both for plain enums
    // (option ids = underlying int values) and enums mapped to a string via a value converter
    // (option ids = provider string values).
    public class EnumDropdownTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public EnumDropdownTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task CreateForm_PlainEnum_RendersSelectWithUnderlyingValuesAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/EnumWidget/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<select id=\"id_Color\" name=\"Color\"", html);
            Assert.Contains("<option value=\"0\">Red</option>", html);
            Assert.Contains("<option value=\"1\">Green</option>", html);
            Assert.Contains("<option value=\"2\">Blue</option>", html);
        }

        [Fact]
        public async Task CreateForm_EnumViaValueConverter_RendersSelectWithProviderValuesAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/EnumWidget/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("<select id=\"id_Size\" name=\"Size\"", html);
            Assert.Contains("<option value=\"small\">Small</option>", html);
            Assert.Contains("<option value=\"medium\">Medium</option>", html);
            Assert.Contains("<option value=\"large\">Large</option>", html);
        }

        [Fact]
        public async Task CreateForm_EnumField_DoesNotRenderPlainTextInputAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/EnumWidget/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.DoesNotContain("<input type=\"text\" id=\"id_Color\"", html);
            Assert.DoesNotContain("<input type=\"text\" id=\"id_Size\"", html);
        }

        [Fact]
        public async Task PostCreate_EnumValues_SucceedsWithoutIntegerValidationErrorAsync()
        {
            // Arrange
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Widget_" + Guid.NewGuid().ToString("N")[..8]),
                new KeyValuePair<string, string>("Color", "1"),        // plain enum -> underlying int id
                new KeyValuePair<string, string>("Size", "medium"),    // converter enum -> provider string id
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/EnumWidget/add/", formData);

            // Assert
            // A successful create redirects; a validation failure would return 200 with the form
            // and the "Enter a valid integer number." message for the string-backed Size field.
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
    }
}
