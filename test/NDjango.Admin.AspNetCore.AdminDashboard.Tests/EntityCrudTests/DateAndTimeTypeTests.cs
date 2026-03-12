using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;

using Xunit;

using NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.EntityCrudTests
{
    public class DateAndTimeTypeTests : IClassFixture<AdminDashboardFixture>
    {
        private readonly HttpClient _client;

        public DateAndTimeTypeTests(AdminDashboardFixture fixture)
        {
            _client = fixture.GetTestHost().GetTestClient();
        }

        private static string ExtractIdFromRedirect(string locationHeader)
        {
            var match = Regex.Match(locationHeader, @"/admin/Gift/(\d+)/change/");
            Assert.True(match.Success, $"Expected redirect to /admin/Gift/{{id}}/change/ but got: {locationHeader}");
            return match.Groups[1].Value;
        }

        private static FormUrlEncodedContent BuildGiftForm(
            string saveAction = "save",
            string name = null,
            string expirationDate = "2027-12-31",
            string availableFrom = "08:00:00",
            string preparationTime = "00:30:00",
            string shippedAt = "2026-03-01T10:00:00+00:00",
            string trackingCode = null,
            string barcode = "1001001",
            string price = "29.99",
            string weight = "0.5",
            string rating = "4.5",
            string quantityInStock = "150",
            string minAge = "3",
            string description = "Test gift",
            string notes = "Test notes",
            string isWrapped = "true")
        {
            name ??= "Gift_" + Guid.NewGuid().ToString("N")[..8];
            trackingCode ??= Guid.NewGuid().ToString();

            return new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Name", name),
                new KeyValuePair<string, string>("ExpirationDate", expirationDate),
                new KeyValuePair<string, string>("AvailableFrom", availableFrom),
                new KeyValuePair<string, string>("PreparationTime", preparationTime),
                new KeyValuePair<string, string>("ShippedAt", shippedAt),
                new KeyValuePair<string, string>("TrackingCode", trackingCode),
                new KeyValuePair<string, string>("Barcode", barcode),
                new KeyValuePair<string, string>("Price", price),
                new KeyValuePair<string, string>("Weight", weight),
                new KeyValuePair<string, string>("Rating", rating),
                new KeyValuePair<string, string>("QuantityInStock", quantityInStock),
                new KeyValuePair<string, string>("MinAge", minAge),
                new KeyValuePair<string, string>("Description", description),
                new KeyValuePair<string, string>("Notes", notes),
                new KeyValuePair<string, string>("IsWrapped", isWrapped),
                new KeyValuePair<string, string>("_save_action", saveAction),
            });
        }

        [Fact]
        public async Task CreateForm_RendersAllGiftFieldsAsync()
        {
            // Arrange & Act
            var response = await _client.GetAsync("/admin/Gift/add/");
            var html = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Add gift", html);
            Assert.Contains("Expiration Date", html);
            Assert.Contains("Available From", html);
            Assert.Contains("Preparation Time", html);
            Assert.Contains("Shipped At", html);
            Assert.Contains("Tracking Code", html);
        }

        [Fact]
        public async Task PostCreate_WithDateOnlyField_SucceedsAsync()
        {
            // Arrange
            var formData = BuildGiftForm(saveAction: "continue", expirationDate: "2028-06-15");

            // Act
            var response = await _client.PostAsync("/admin/Gift/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            ExtractIdFromRedirect(location);

            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            // DateOnly may render in locale format (dd/MM/yyyy) or ISO (yyyy-MM-dd)
            Assert.True(editHtml.Contains("2028-06-15") || editHtml.Contains("15/06/2028"),
                "Expected DateOnly value 2028-06-15 in edit form");
        }

        [Fact]
        public async Task PostCreate_WithTimeOnlyField_SucceedsAsync()
        {
            // Arrange
            var formData = BuildGiftForm(saveAction: "continue", availableFrom: "14:30:00");

            // Act
            var response = await _client.PostAsync("/admin/Gift/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            ExtractIdFromRedirect(location);

            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            Assert.Contains("14:30", editHtml);
        }

        [Fact]
        public async Task PostCreate_WithTimeSpanField_SucceedsAsync()
        {
            // Arrange
            var formData = BuildGiftForm(saveAction: "continue", preparationTime: "02:15:00");

            // Act
            var response = await _client.PostAsync("/admin/Gift/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            ExtractIdFromRedirect(location);

            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            Assert.Contains("02:15:00", editHtml);
        }

        [Fact]
        public async Task PostCreate_WithAllFieldTypes_SucceedsAndRedirectsAsync()
        {
            // Arrange
            var formData = BuildGiftForm(saveAction: "save");

            // Act
            var response = await _client.PostAsync("/admin/Gift/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/admin/Gift/", response.Headers.Location.ToString());
        }

        [Fact]
        public async Task PostCreate_WithAllFieldTypes_SaveAndContinue_RendersEditFormAsync()
        {
            // Arrange
            var name = "AllTypes_" + Guid.NewGuid().ToString("N")[..8];
            var formData = BuildGiftForm(
                saveAction: "continue",
                name: name,
                expirationDate: "2029-01-15",
                availableFrom: "09:45:00",
                preparationTime: "01:30:00",
                barcode: "5005005",
                price: "99.95",
                weight: "2.3",
                rating: "4.8",
                quantityInStock: "42",
                minAge: "12",
                description: "Full type test",
                notes: "All fields populated");

            // Act
            var response = await _client.PostAsync("/admin/Gift/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            Assert.Matches(@"/admin/Gift/\d+/change/", location);

            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            Assert.Contains(name, editHtml);
            Assert.Contains("09:45", editHtml);
            Assert.Contains("01:30:00", editHtml);
            Assert.Contains("5005005", editHtml);
            Assert.Contains("Full type test", editHtml);
        }

        [Fact]
        public async Task PostUpdate_DateOnlyField_PersistsNewValueAsync()
        {
            // Arrange — create a record first
            var createForm = BuildGiftForm(saveAction: "continue", expirationDate: "2027-01-01");
            var createResponse = await _client.PostAsync("/admin/Gift/add/", createForm);
            var location = createResponse.Headers.Location.ToString();
            var id = ExtractIdFromRedirect(location);

            // Act — update with a new date
            var updateForm = BuildGiftForm(saveAction: "continue", expirationDate: "2030-12-25");
            var updateResponse = await _client.PostAsync($"/admin/Gift/{id}/change/", updateForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, updateResponse.StatusCode);
            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            // DateOnly may render in locale format (dd/MM/yyyy) or ISO (yyyy-MM-dd)
            Assert.True(editHtml.Contains("2030-12-25") || editHtml.Contains("25/12/2030"),
                "Expected updated DateOnly value 2030-12-25 in edit form");
        }

        [Fact]
        public async Task PostUpdate_TimeOnlyField_PersistsNewValueAsync()
        {
            // Arrange — create a record first
            var createForm = BuildGiftForm(saveAction: "continue", availableFrom: "06:00:00");
            var createResponse = await _client.PostAsync("/admin/Gift/add/", createForm);
            var location = createResponse.Headers.Location.ToString();
            var id = ExtractIdFromRedirect(location);

            // Act — update with a new time
            var updateForm = BuildGiftForm(saveAction: "continue", availableFrom: "22:30:00");
            var updateResponse = await _client.PostAsync($"/admin/Gift/{id}/change/", updateForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, updateResponse.StatusCode);
            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            Assert.Contains("22:30", editHtml);
        }

        [Fact]
        public async Task PostUpdate_TimeSpanField_PersistsNewValueAsync()
        {
            // Arrange — create a record first
            var createForm = BuildGiftForm(saveAction: "continue", preparationTime: "00:10:00");
            var createResponse = await _client.PostAsync("/admin/Gift/add/", createForm);
            var location = createResponse.Headers.Location.ToString();
            var id = ExtractIdFromRedirect(location);

            // Act — update with a new timespan
            var updateForm = BuildGiftForm(saveAction: "continue", preparationTime: "05:00:00");
            var updateResponse = await _client.PostAsync($"/admin/Gift/{id}/change/", updateForm);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, updateResponse.StatusCode);
            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            Assert.Contains("05:00:00", editHtml);
        }

        [Fact]
        public async Task PostCreate_WithDateTimeOffsetField_SucceedsAsync()
        {
            // Arrange
            var formData = BuildGiftForm(saveAction: "continue", shippedAt: "2026-05-20T15:30:00+00:00");

            // Act
            var response = await _client.PostAsync("/admin/Gift/add/", formData);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location.ToString();
            ExtractIdFromRedirect(location);

            var editHtml = await (await _client.GetAsync(location)).Content.ReadAsStringAsync();
            Assert.Contains("2026", editHtml);
            Assert.Contains("15:30", editHtml);
        }

        [Fact]
        public async Task ListPage_AfterCreatingGift_ShowsRecordAsync()
        {
            // Arrange
            var name = "ListCheck_" + Guid.NewGuid().ToString("N")[..8];
            var formData = BuildGiftForm(saveAction: "save", name: name);

            // Act
            await _client.PostAsync("/admin/Gift/add/", formData);
            var listResponse = await _client.GetAsync("/admin/Gift/");
            var html = await listResponse.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            Assert.Contains(name, html);
        }
    }
}
