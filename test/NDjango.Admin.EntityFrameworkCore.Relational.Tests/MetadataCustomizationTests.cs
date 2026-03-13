using System.Linq;

using Xunit;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    /// <summary>
    /// Test building metadata with NDjango.Admin options.
    /// </summary>
    public class MetadataCustomizationTests
    {
        private readonly TestDbContext _dbContext;

        public MetadataCustomizationTests()
        {
            _dbContext = TestDbContext.Create();
        }

        /// <summary>
        /// Test customizing entities metadata.
        /// </summary>
        [Fact]
        public void TestCustomizingEntitiesMetadata()
        {
            var displayName = Faker.Lorem.Sentence();
            var displayNamePlural = Faker.Lorem.Sentence();
            var description = Faker.Lorem.Sentence();
            var enabled = Faker.Boolean.Random();
            var secondDisplayName = Faker.Lorem.Sentence();
            var editable = Faker.Boolean.Random();

            var loaderOptions = new DbContextMetaDataLoaderOptions();
            loaderOptions.CustomizeModel(model =>
            {
                model.Entity<Category>()
                    .SetDisplayName(displayName)
                    .SetDisplayNamePlural(displayNamePlural)
                    .SetDescription(description)
                    .SetEditable(editable);

                model.Entity<Customer>()
                    .SetDisplayName(secondDisplayName);
            });

            Assert.NotNull(loaderOptions.ModelCustomizer);

            var metaData = new MetaData();
            metaData.LoadFromDbContext(_dbContext, loaderOptions);

            var entity = metaData.EntityRoot.FindSubEntity(e => e.ClrType == typeof(Category));
            Assert.NotNull(entity);
            Assert.Equal(displayName, entity.Name);
            Assert.Equal(displayNamePlural, entity.NamePlural);
            Assert.Equal(description, entity.Description);
            Assert.Equal(editable, entity.IsEditable);

            entity = metaData.EntityRoot.FindSubEntity(e => e.ClrType == typeof(Customer));
            Assert.NotNull(entity);
            Assert.Equal(secondDisplayName, entity.Name);
        }

        /// <summary>
        /// Test customizing entity attributes metadata.
        /// </summary>
        [Fact]
        public void TestCustomizingEntityAttributesMetadata()
        {
            var displayName = Faker.Lorem.Sentence();
            var description = Faker.Lorem.Sentence();
            var showInLookup = Faker.Boolean.Random();
            var editable = Faker.Boolean.Random();
            var showOnCreate = Faker.Boolean.Random();
            var sorting = Faker.RandomNumber.Next();
            var enabled = Faker.Boolean.Random();
            var showOnView = Faker.Boolean.Random();
            var index = Faker.RandomNumber.Next();
            var showOnEdit = Faker.Boolean.Random();
            var secondDescription = Faker.Lorem.Sentence();

            var loaderOptions = new DbContextMetaDataLoaderOptions();

            loaderOptions.CustomizeModel(builder =>
            {
                builder.Entity<Category>().Attribute(e => e.Description)
                    .SetDisplayName(displayName)
                    .SetDescription(description)
                    .SetEditable(editable)
                    .SetShowOnView(showOnView)
                    .SetShowOnCreate(showOnCreate)
                    .SetShowOnEdit(showOnEdit)
                    .SetShowInLookup(showInLookup)
                    .SetSorting(sorting)
                    .SetIndex(index);

                builder.Entity<Order>().Attribute(e => e.Id)
                    .SetDescription(secondDescription);
            });

            Assert.NotNull(loaderOptions.ModelCustomizer);

            var metaData = new MetaData();
            metaData.LoadFromDbContext(_dbContext, loaderOptions);

            var entity = metaData.EntityRoot.FindSubEntity(e => e.ClrType == typeof(Category));
            Assert.NotNull(entity);
            var attr = entity.FindAttribute(a => a.PropName == "Description");
            Assert.NotNull(attr);
            Assert.Equal(displayName, attr.Caption);
            Assert.Equal(description, attr.Description);
            Assert.Equal(editable, attr.IsEditable);
            Assert.Equal(showOnView, attr.ShowOnView);
            Assert.Equal(showOnCreate, attr.ShowOnCreate);
            Assert.Equal(showOnEdit, attr.ShowOnEdit);
            Assert.Equal(showInLookup, attr.ShowInLookup);
            Assert.Equal(sorting, attr.Sorting);
            Assert.Equal(index, attr.Index);

            entity = metaData.EntityRoot.FindSubEntity(e => e.ClrType == typeof(Order));
            Assert.NotNull(entity);
            attr = entity.FindAttribute(a => a.PropName == "Id");
            Assert.NotNull(attr);
            Assert.Equal(secondDescription, attr.Description);
        }
    }
}
