using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;


using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace NDjango.Admin.AspNetCore.Tests
{
    public class NDjangoAdminMiddlewareTests : IClassFixture<NDjangoAdminMiddlewareFixture>
    {
        private readonly IHost _host;

        public NDjangoAdminMiddlewareTests(NDjangoAdminMiddlewareFixture fixture)
        {
            _host = fixture.GetTestHost();
        }

        [Theory]
        [InlineData("/api/ndjango-admin")]
        [InlineData("/api/data")]
        public async Task NDjangoAdmin_GetModel_should_return_model(string endpoint)
        {
            var client = _host.GetTestClient();
            var response = await client.GetAsync($"{endpoint}/models/__default");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("application/json", response.Content.Headers.ContentType.ToString());

            var model = new MetaData();

            var jsonReader = new JsonTextReader(new StreamReader(await response.Content.ReadAsStreamAsync()));
            var responseObj = JObject.Load(jsonReader);

            var modelObj = responseObj["model"];
            Assert.NotNull(modelObj);

            await model.ReadFromJsonAsync(modelObj.CreateReader(), MetaDataReadWriteOptions.Defaults);

            Assert.Equal(8, model.EntityRoot.SubEntities.Count);
            Assert.Contains(model.EntityRoot.SubEntities, ent => ent.Id == "Category");
        }

        [Theory]
        [InlineData("/api/ndjango-admin", "Customer", 91)]
        [InlineData("/api/data", "Customer", 91)]
        [InlineData("/api/ndjango-admin", "Product", 77)]
        [InlineData("/api/data", "Product", 77)]
        public async Task NDjangoAdmin_FetchRecords_should_return_resultSet_with_records(string endpoint, string sourceId, int count)
        {
            var client = _host.GetTestClient();
            var response = await client.PostAsync($"{endpoint}/models/__default/sources/{sourceId}/fetch", new StringContent("{ \"needTotal\": true }"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.StartsWith("application/json", response.Content.Headers.ContentType.ToString());

            var jsonReader = new JsonTextReader(new StreamReader(await response.Content.ReadAsStreamAsync()));
            var responseObj = JObject.Load(jsonReader);

            var resultSet = responseObj["resultSet"];
            Assert.NotNull(resultSet);

            var meta = responseObj["meta"];
            Assert.NotNull(meta);
            Assert.Equal(count, meta["totalRecords"].ToObject<int>());
        }

        [Theory]
        [InlineData("/api/ndjango-admin", "Customer", "Id", "ALFKI")]
        [InlineData("/api/data", "Customer", "Id", "ALFKI")]
        [InlineData("/api/ndjango-admin", "Product", "Id", "1")]
        [InlineData("/api/data", "Product", "Id", "1")]
        public async Task NDjangoAdmin_FetchRecord_should_return_record(string endpoint, string sourceId, string keyProperty, string recordId)
        {
            var client = _host.GetTestClient();
            var response = await client.GetAsync($"{endpoint}/models/__default/sources/{sourceId}/fetch?{keyProperty}={recordId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.StartsWith("application/json", response.Content.Headers.ContentType.ToString());

            var responseContentStream = await response.Content.ReadAsStreamAsync();
            var jsonReader = new JsonTextReader(new StreamReader(responseContentStream));
            var responseObj = JObject.Load(jsonReader);

            var entityObj = responseObj["record"];
            Assert.NotNull(entityObj);
        }

        [Theory]
        [MemberData(nameof(GetAddRecordData))]
        public async Task NDjangoAdmin_CreateRecord_should_create_record(string endpoint, string sourceId, JObject data)
        {
            var client = _host.GetTestClient();
            var content = new StringContent(data.ToString());
            var response = await client.PostAsync($"{endpoint}/models/__default/sources/{sourceId}/create", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.StartsWith("application/json", response.Content.Headers.ContentType.ToString());

            var dbContext = _host.Services.GetRequiredService<TestDbContext>();

            if (sourceId == "Category")
            {
                var id = data["Id"].ToObject<int>();
                var result = await dbContext.Set<Category>().FindAsync(id);
                Assert.NotNull(result);
                CompareWithJObject(result, data);
            }
            else if (sourceId == "Shipper")
            {
                var id = data["Id"].ToObject<int>();
                var result = await dbContext.Set<Shipper>().FindAsync(id);
                Assert.NotNull(result);
                CompareWithJObject(result, data);
            }
        }

        private void CompareWithJObject(object obj, JObject jobj)
        {
            foreach (var kv in jobj)
            {

                var prop = obj.GetType().GetProperty(kv.Key);
                if (prop != null)
                {
                    Assert.Equal(kv.Value.ToObject(prop.PropertyType), prop.GetValue(obj));
                }
            }
        }

        public static IEnumerable<object[]> GetAddRecordData()
            => new List<object[]>() {
                new object[] {
                    "/api/ndjango-admin",
                    "Category",
                    new JObject() {
                        ["Id"] = 20,
                        ["CategoryName"] = "Test 20"
                    }
                },
                new object[] {
                    "/api/data",
                    "Category",
                    new JObject() {
                        ["Id"] = 40,
                        ["CategoryName"] = "Test 40"
                    }
                },
                new object[] {
                    "/api/ndjango-admin",
                    "Shipper",
                    new JObject() {
                        ["Id"] = 20,
                        ["CompanyName"] = "Test 20"
                    }
                },
                new object[] {
                    "/api/ndjango-admin",
                    "Shipper",
                    new JObject() {
                        ["Id"] = 40,
                        ["CompanyName"] = "Test 40"
                    }
                }
            };

        [Theory]
        [MemberData(nameof(GetUpdateRecordData))]
        public async Task NDjangoAdmin_UpdateRecord_should_update_record(string endpoint, string sourceId, JObject data)
        {
            var client = _host.GetTestClient();
            var content = new StringContent(data.ToString());
            var response = await client.PostAsync($"{endpoint}/models/__default/sources/{sourceId}/update", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.StartsWith("application/json", response.Content.Headers.ContentType.ToString());

            var dbContext = _host.Services.GetRequiredService<TestDbContext>();

            if (sourceId == "Category")
            {
                var id = data["Id"].ToObject<int>();
                var result = await dbContext.Set<Category>().FindAsync(id);
                Assert.NotNull(result);
                CompareWithJObject(result, data);
            }
            else if (sourceId == "Employee")
            {
                var id = data["Id"].ToObject<int>();
                var result = await dbContext.Set<Employee>().FindAsync(id);
                Assert.NotNull(result);
                CompareWithJObject(result, data);
            }
        }

        public static IEnumerable<object[]> GetUpdateRecordData()
            => new List<object[]>() {
                new object[] {
                    "/api/ndjango-admin",
                    "Category",
                    new JObject() {
                        ["Id"] = 4,
                        ["CategoryName"] = "Test 4"
                    }
                },
                new object[] {
                    "/api/data",
                    "Category",
                    new JObject() {
                        ["Id"] = 5,
                        ["CategoryName"] = "Test 5"
                    }
                },
                new object[] {
                    "/api/ndjango-admin",
                    "Employee",
                    new JObject() {
                        ["Id"] = 1,
                        ["LastName"] = "Test 1"
                    }
                },
                new object[] {
                    "/api/ndjango-admin",
                    "Employee",
                    new JObject() {
                        ["Id"] = 2,
                        ["LastName"] = "Test 2"
                    }
                }
            };

        [Theory]
        [InlineData("/api/ndjango-admin", "Category", "Id", "1")]
        [InlineData("/api/data", "Category", "Id", "2")]
        [InlineData("/api/ndjango-admin", "Shipper", "Id", "1")]
        [InlineData("/api/data", "Shipper", "Id", "2")]
        public async Task NDjangoAdmin_DeleteRecord_should_delete_record(string endpoint, string sourceId, string keyPropery, string entityId)
        {
            var client = _host.GetTestClient();
            var content = new StringContent($"{{\"{keyPropery}\": {entityId}}}");
            var response = await client.PostAsync($"{endpoint}/models/__default/sources/{sourceId}/delete", content);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.StartsWith("application/json", response.Content.Headers.ContentType.ToString());

            var dbContext = _host.Services.GetRequiredService<TestDbContext>();

            if (sourceId == "Category")
            {
                var result = await dbContext.Set<Category>().FindAsync(int.Parse(entityId));
                Assert.Null(result);
            }
            else if (sourceId == "Shipper")
            {
                var result = await dbContext.Set<Shipper>().FindAsync(int.Parse(entityId));
                Assert.Null(result);
            }
        }
    }
}
