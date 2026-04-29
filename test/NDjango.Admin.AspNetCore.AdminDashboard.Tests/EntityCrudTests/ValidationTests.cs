using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    public class ValidationTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public ValidationTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetAuthenticatedClient();
        }

        [Fact]
        public async Task CreateForm_StringWithMaxLength_EmitsMaxlengthAttributeAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/ValidatedProduct/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("maxlength=\"50\"", html);
        }

        [Fact]
        public async Task CreateForm_IntRange_EmitsMinMaxAttributesAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/ValidatedProduct/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("min=\"1\"", html);
            Assert.Contains("max=\"1000\"", html);
        }

        [Fact]
        public async Task CreateForm_RegexWithoutInlineFlags_EmitsPatternAttributeAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/ValidatedProduct/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("pattern=\"\\d{5}\"", html);
            Assert.Contains("title=\"Must be 5 digits\"", html);
        }

        [Fact]
        public async Task CreateForm_RegexWithInlineFlags_DoesNotEmitPatternAttributeAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/ValidatedProduct/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.DoesNotContain("(?i)", html);
        }

        [Fact]
        public async Task CreateForm_EmailField_RendersAsEmailInputAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/ValidatedProduct/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("type=\"email\"", html);
        }

        [Fact]
        public async Task CreateForm_UrlField_RendersAsUrlInputAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/ValidatedProduct/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Contains("type=\"url\"", html);
        }

        [Fact]
        public async Task CreateForm_DecimalWithScale_EmitsStepMatchingScaleAsync()
        {
            // Arrange — MenuItem.Price is configured as decimal(10,2); step should match the scale.

            // Act
            var response = await _client.GetAsync("/admin/MenuItem/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("step=\"0.01\"", html);
        }

        [Fact]
        public async Task CreateForm_FloatWithoutScale_EmitsStepAnyAsync()
        {
            // Arrange — Gift.Weight is a double with no HasPrecision, so the step must fall back to "any"
            // so browsers accept arbitrary fractional digits instead of rejecting non-integer input.

            // Act
            var response = await _client.GetAsync("/admin/Gift/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("step=\"any\"", html);
        }

        [Fact]
        public async Task CreatePost_MissingRequiredField_Returns400WithErrorlistAsync()
        {
            // Arrange
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Quantity", "5"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("errorlist", html);
            Assert.Contains("This field is required.", html);
        }

        [Fact]
        public async Task CreatePost_StringExceedsMaxLength_Returns400WithAtMostMessageAsync()
        {
            // Arrange — Name is capped at 50 chars; submit 60
            var longName = new string('x', 60);
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", longName),
                new KeyValuePair<string, string>("Quantity", "5"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("at most 50 characters", html);
        }

        [Fact]
        public async Task CreatePost_QuantityOutOfRange_Returns400Async()
        {
            // Arrange
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "MyProduct"),
                new KeyValuePair<string, string>("Quantity", "5000"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("less than or equal to 1000", html);
        }

        [Fact]
        public async Task CreatePost_InvalidEmail_Returns400Async()
        {
            // Arrange
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Prod"),
                new KeyValuePair<string, string>("Quantity", "5"),
                new KeyValuePair<string, string>("Email", "not-an-email"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("valid email", html);
        }

        [Fact]
        public async Task CreatePost_RegexWithInlineFlags_ServerStillEnforcesAsync()
        {
            // Arrange — CaseInsensitive requires "allowed" (pattern skipped from HTML but
            // FieldValidator still runs it).
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Prod"),
                new KeyValuePair<string, string>("Quantity", "5"),
                new KeyValuePair<string, string>("CaseInsensitive", "disallowed"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", form);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreatePost_InvalidSubmission_PreservesSubmittedValuesAsync()
        {
            // Arrange — valid Name but invalid Quantity; Name should round-trip.
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "ShouldStickAround"),
                new KeyValuePair<string, string>("Quantity", "99999"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("ShouldStickAround", html);
        }

        [Fact]
        public async Task CreatePost_AllValidFields_RedirectsAsync()
        {
            // Arrange
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Valid"),
                new KeyValuePair<string, string>("Quantity", "42"),
                new KeyValuePair<string, string>("Email", "ok@example.com"),
                new KeyValuePair<string, string>("Website", "https://example.com"),
                new KeyValuePair<string, string>("PostalCode", "12345"),
                new KeyValuePair<string, string>("CaseInsensitive", "ALLOWED"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", form);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task EditForm_NonExistentId_Returns404Async()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/Category/99999/change/");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task EditForm_NonNumericId_Returns404Async()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/Category/abc/change/");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeleteForm_NonExistentId_Returns404Async()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/Category/99999/delete/");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeleteForm_NonNumericId_Returns404Async()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/Category/abc/delete/");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task List_InvalidSortColumn_FallsBackToDefaultSortAsync()
        {
            // Arrange
            // No per-test setup required; _client is created once in the constructor.

            // Act
            var response = await _client.GetAsync("/admin/Category/?sort=NotAColumn");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Italian", html);
        }

        [Fact]
        public async Task CreatePost_NonExistentForeignKey_Returns400WithErrorAsync()
        {
            // Arrange — FK to a Restaurant that doesn't exist (9999)
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Website", "https://orphan.example.com"),
                new KeyValuePair<string, string>("Phone", "555-0199"),
                new KeyValuePair<string, string>("RestaurantId", "9999"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/RestaurantProfile/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Referenced record does not exist", html);
        }

        [Fact]
        public async Task CreatePost_UniqueConstraintViolation_Returns400WithErrorAsync()
        {
            // Arrange — seed already has a RestaurantProfile for Restaurant 1; add another for same FK
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Website", "https://dup.example.com"),
                new KeyValuePair<string, string>("Phone", "555-9999"),
                new KeyValuePair<string, string>("RestaurantId", "1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/RestaurantProfile/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("already exists", html);
        }

        [Fact]
        public async Task CreatePost_DecimalOverflowPrecision_Returns400Async()
        {
            // Arrange — MenuItem.Price is decimal(10,2); 999999999.99 has 9 integer digits
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Overflow"),
                new KeyValuePair<string, string>("Price", "999999999.99"),
                new KeyValuePair<string, string>("RestaurantId", "1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/MenuItem/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("digits before the decimal point", html);
        }

        [Fact]
        public async Task CreatePost_NonNumericDecimal_Returns400Async()
        {
            // Arrange
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Bad Price"),
                new KeyValuePair<string, string>("Price", "not-a-number"),
                new KeyValuePair<string, string>("RestaurantId", "1"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/MenuItem/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("valid number", html);
        }

        [Fact]
        public async Task CreatePost_InvalidGuidScalar_Returns400Async()
        {
            // Arrange — Gift.TrackingCode is a Guid
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Test Gift"),
                new KeyValuePair<string, string>("TrackingCode", "not-a-guid"),
                new KeyValuePair<string, string>("Price", "1.00"),
                new KeyValuePair<string, string>("Barcode", "1"),
                new KeyValuePair<string, string>("Weight", "1"),
                new KeyValuePair<string, string>("Rating", "1"),
                new KeyValuePair<string, string>("QuantityInStock", "1"),
                new KeyValuePair<string, string>("MinAge", "1"),
                new KeyValuePair<string, string>("ShippedAt", "2025-01-01"),
                new KeyValuePair<string, string>("PreparationTime", "00:10:00"),
                new KeyValuePair<string, string>("ExpirationDate", "2025-12-31"),
                new KeyValuePair<string, string>("AvailableFrom", "08:00"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Gift/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("valid UUID", html);
        }

        [Fact]
        public async Task CreatePost_ByteOverflow_Returns400Async()
        {
            // Arrange — Gift.MinAge is a byte (0-255); submit 999
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Test Gift"),
                new KeyValuePair<string, string>("TrackingCode", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("Price", "1.00"),
                new KeyValuePair<string, string>("Barcode", "1"),
                new KeyValuePair<string, string>("Weight", "1"),
                new KeyValuePair<string, string>("Rating", "1"),
                new KeyValuePair<string, string>("QuantityInStock", "1"),
                new KeyValuePair<string, string>("MinAge", "999"),
                new KeyValuePair<string, string>("ShippedAt", "2025-01-01"),
                new KeyValuePair<string, string>("PreparationTime", "00:10:00"),
                new KeyValuePair<string, string>("ExpirationDate", "2025-12-31"),
                new KeyValuePair<string, string>("AvailableFrom", "08:00"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/Gift/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("between 0 and 255", html);
        }

        [Fact]
        public async Task CreatePost_StringWithNullByte_Returns400WithInvalidCharactersErrorAsync()
        {
            // Arrange — ASP.NET Core FormPipeReader rejects %00 in form decode; surface a 400 instead of propagating HTTP 500
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "Good\0Name"),
                new KeyValuePair<string, string>("Quantity", "5"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", form);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("invalid characters", html);
        }

        [Fact]
        public async Task CreatePost_JsonContentType_Returns400WithInvalidContentTypeErrorAsync()
        {
            // Arrange — ReadFormAsync throws InvalidOperationException when Content-Type is not form; surface a 400 instead of HTTP 500
            var content = new StringContent("{\"Name\":\"Test\",\"Quantity\":1}", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/admin/ValidatedProduct/add/", content);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("invalid content type", html);
        }

        [Fact]
        public async Task UpdatePost_JsonContentType_Returns400WithInvalidContentTypeErrorAsync()
        {
            // Arrange — use a pre-seeded record; the content-type guard must run before any DB work
            var content = new StringContent("{\"Name\":\"Changed\"}", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/admin/Category/1/change/", content);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("invalid content type", html);
        }

        [Fact]
        public async Task UpdatePost_ValidationFailure_Returns400AndRendersFormWithErrorsAsync()
        {
            // Arrange — seed a Category, then attempt to update it with an invalid (too long) name.
            // Covers the RazorViewDispatcher.RenderEntityFormWithErrorsAsync edit-path: must set HTTP 400
            // and re-render the form including the errorlist, not redirect.
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "UpdateValidation_" + Guid.NewGuid().ToString("N")[..8]),
                new KeyValuePair<string, string>("Quantity", "10"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResp = await _client.PostAsync("/admin/ValidatedProduct/add/", createForm);
            Assert.Equal(HttpStatusCode.Redirect, createResp.StatusCode);
            var id = System.Text.RegularExpressions.Regex
                .Match(createResp.Headers.Location.ToString(), @"/admin/ValidatedProduct/(\d+)/change/")
                .Groups[1].Value;

            var longName = new string('z', 60);
            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", longName),
                new KeyValuePair<string, string>("Quantity", "10"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync($"/admin/ValidatedProduct/{id}/change/", updateForm);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("errorlist", html);
            Assert.Contains("at most 50 characters", html);
            Assert.Contains("entity-form", html); // confirms a bound form re-render, not a redirect
        }

        [Fact]
        public async Task UpdatePost_ValidationFailure_PreservesSubmittedValuesAsync()
        {
            // Arrange — on edit, a failed validation must re-render the form with the user's submitted
            // values (not the stored record's values), mirroring Django's bound-form behavior.
            var uniqueValidPart = "SubmittedOnEdit_" + Guid.NewGuid().ToString("N")[..6];
            var createForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", uniqueValidPart),
                new KeyValuePair<string, string>("Quantity", "10"),
                new KeyValuePair<string, string>("_save_action", "continue"),
            });
            var createResp = await _client.PostAsync("/admin/ValidatedProduct/add/", createForm);
            var id = System.Text.RegularExpressions.Regex
                .Match(createResp.Headers.Location.ToString(), @"/admin/ValidatedProduct/(\d+)/change/")
                .Groups[1].Value;

            var updateForm = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", "KeepThisTyped"),
                new KeyValuePair<string, string>("Quantity", "99999"),
                new KeyValuePair<string, string>("_save_action", "save"),
            });

            // Act
            var response = await _client.PostAsync($"/admin/ValidatedProduct/{id}/change/", updateForm);
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("KeepThisTyped", html);
            Assert.DoesNotContain(uniqueValidPart, html);
        }
    }
}
