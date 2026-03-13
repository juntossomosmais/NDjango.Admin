using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    /// <summary>
    /// Tests merging metadata from options with Model.
    /// </summary>
    public class MetadataAnnotationsPlusCustomizationTests
    {
        private readonly DbContext _dbContext;

        /// <summary>
        /// Get db context.
        /// </summary>
        public MetadataAnnotationsPlusCustomizationTests()
        {
            _dbContext = TestDbContext.Create();
        }

        /// <summary>
        /// Test getting entity metadata.
        /// </summary>
        [Fact]
        public void TestGetEntityMeta()
        {
            var loaderOptions = new DbContextMetaDataLoaderOptions();
            var optionsDisplayName = Faker.Lorem.Sentence();
            var optionsDisplayNamePlural = Faker.Lorem.Sentence();
            var editable = Faker.Boolean.Random();

            loaderOptions.CustomizeModel(model =>
            {
                model.Entity<Category>().SetDisplayName(optionsDisplayName)
                    .SetDisplayNamePlural(optionsDisplayNamePlural).SetEditable(editable);
            });

            var metaData = new MetaData();
            metaData.LoadFromDbContext(_dbContext, loaderOptions);

            var entity = metaData.EntityRoot.FindSubEntity(e => e.ClrType == typeof(Category));
            Assert.Equal(optionsDisplayName, entity.Name);
            Assert.Equal(optionsDisplayNamePlural, entity.NamePlural);
            Assert.Equal("Categories description", entity.Description);
            Assert.Equal(editable, entity.IsEditable);
        }

        /// <summary>
        /// Test getting entity attribute metadata.
        /// </summary>
        [Fact]
        public void TestGetEntityAttributeMeta()
        {
            var loaderOptions = new DbContextMetaDataLoaderOptions();

            var optionsDisplayName = Faker.Lorem.Sentence();
            var optionsDescription = Faker.Lorem.Sentence();

            loaderOptions.CustomizeModel(model =>
            {
                model.Entity<Category>()
                    .Attribute(category => category.Description)
                    .SetDisplayName(optionsDisplayName)
                    .SetDescription(optionsDescription);
            });

            var metaData = new MetaData();
            metaData.LoadFromDbContext(_dbContext, loaderOptions);

            var entity = metaData.EntityRoot.SubEntities.First(e => e.ClrType == typeof(Category));
            var attribute = entity.Attributes.First(a => a.PropInfo.Name == nameof(Category.Description));
            Assert.Equal(optionsDisplayName, attribute.Caption);
            Assert.Equal(optionsDescription, attribute.Description);
            Assert.Equal(2, attribute.Index);
        }
    }
}
