using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NDjango.Admin.Services;
using Newtonsoft.Json;
using Xunit;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    /// <summary>
    /// Search fields that cross a relationship — Django's <c>search_fields = ["supplier__name"]</c>.
    /// </summary>
    /// <remarks>
    /// A join table is identified by what it points at and never by a column of its own, so
    /// declaring only its own properties leaves the foreign key as the one searchable thing on it.
    /// Finding a row then means looking the parent up on another screen and pasting its id back.
    /// <para>
    /// The recursion this relies on already existed — <c>CreateSubExpression</c> has descended into
    /// navigation properties whenever <c>Depth</c> allowed it. What was missing is a way to say
    /// <em>which</em> relationship, and how far: the property filter saw a <c>PropertyInfo</c> with
    /// no path, so it could not tell <c>Name</c> on the root from <c>Name</c> one hop away, and
    /// raising the depth would have turned any search into a search over the whole object graph.
    /// </para>
    /// </remarks>
    public class SubstringFilterRelationshipTests
    {
        private readonly MetaData _meta;
        private readonly MetaEntity _productEntity;
        private readonly List<Product> _products;

        public SubstringFilterRelationshipTests()
        {
            var dbContext = TestDbContext.Create();
            _meta = new MetaData();
            _meta.LoadFromDbContext(dbContext);
            _productEntity = _meta.EntityRoot.SubEntities.First(e => e.ClrType == typeof(Product));

            var acme = new Supplier { Id = 1, CompanyName = "Acme Trading", City = "Lisbon" };
            var globex = new Supplier { Id = 2, CompanyName = "Globex", City = "Porto" };

            _products = new List<Product>
            {
                new() { Id = 1, Name = "Chai", SupplierID = 1, Supplier = acme },
                new() { Id = 2, Name = "Syrup", SupplierID = 2, Supplier = globex },
            };
        }

        [Fact]
        public async Task Apply_WithAPathThroughARelationship_ShouldMatchTheRelatedColumn()
        {
            // Arrange: the whole point. "Acme" is nowhere on Product — it is the supplier's company
            // name, one relationship away — and before this the only way to find that row was to
            // look the supplier up elsewhere and search by its id.
            SetSearchFields(_productEntity, s_supplierNameField);
            var filter = await FilterFor("acme");

            // Act
            var found = Apply(filter);

            // Assert
            var product = Assert.Single(found);
            Assert.Equal("Chai", product.Name);
        }

        [Fact]
        public async Task Apply_WithAPathThroughARelationship_ShouldNotMatchOtherColumnsOfTheRelatedEntity()
        {
            // Arrange: 🔴 the assertion that makes raising the depth safe. Only the declared column
            // is searched — the supplier's city is one property away from the one that was asked
            // for, and a filter that descended into the relationship wholesale would match it.
            SetSearchFields(_productEntity, s_supplierNameField);
            var filter = await FilterFor("lisbon");

            // Act
            var found = Apply(filter);

            // Assert
            Assert.Empty(found);
        }

        [Fact]
        public async Task Apply_WithAFlatFieldOnly_ShouldNotDescendIntoAnyRelationship()
        {
            // Arrange: the behaviour every existing caller relies on. A declaration with no path
            // leaves the depth at zero, so nothing is searched beyond the entity itself — "globex"
            // is a supplier's name and must stay invisible from a search over Product.Name.
            SetSearchFields(_productEntity, s_ownNameField);
            var filter = await FilterFor("globex");

            // Act
            var found = Apply(filter);

            // Assert
            Assert.Empty(found);
        }

        [Fact]
        public async Task Apply_WithBothAFlatFieldAndAPath_ShouldMatchEither()
        {
            // Arrange: the two live side by side, which is how a join table that does have one
            // useful column of its own gets to keep it.
            SetSearchFields(_productEntity, s_bothFields);

            // Act
            var byOwnColumn = Apply(await FilterFor("syrup"));
            var byRelated = Apply(await FilterFor("acme"));

            // Assert
            Assert.Equal("Syrup", Assert.Single(byOwnColumn).Name);
            Assert.Equal("Chai", Assert.Single(byRelated).Name);
        }

        [Fact]
        public async Task Apply_WithAPathWhoseRelationshipIsNull_ShouldNotThrow()
        {
            // Arrange: a nullable foreign key is the ordinary case — a product with no supplier
            // yet. The expression guards the navigation before reading through it, so this is a
            // row that does not match rather than a NullReferenceException mid-query.
            _products.Add(new Product { Id = 3, Name = "Orphan", SupplierID = null, Supplier = null });
            SetSearchFields(_productEntity, s_supplierNameField);
            var filter = await FilterFor("acme");

            // Act
            var found = Apply(filter);

            // Assert
            Assert.Equal("Chai", Assert.Single(found).Name);
        }

        private static readonly string[] s_supplierNameField = ["Supplier.CompanyName"];
        private static readonly string[] s_ownNameField = ["Name"];
        private static readonly string[] s_bothFields = ["Name", "Supplier.CompanyName"];

        private async Task<SubstringFilter> FilterFor(string text)
        {
            var filter = new SubstringFilter(_meta);
            using var sr = new StringReader($"{{\"value\": \"{text}\"}}");
            using var reader = new JsonTextReader(sr);
            await filter.ReadFromJsonAsync(reader);
            return filter;
        }

        private List<Product> Apply(SubstringFilter filter) =>
            ((IQueryable<Product>)filter.Apply(_productEntity, isLookup: false, _products.AsQueryable()))
            .ToList();

        private static void SetSearchFields(MetaEntity entity, IReadOnlyList<string> fields) =>
            typeof(MetaEntity).GetProperty("SearchFields").SetValue(entity, fields);
    }
}
