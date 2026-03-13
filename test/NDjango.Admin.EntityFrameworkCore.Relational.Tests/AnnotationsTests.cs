using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;

using Xunit;


namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    public class AnnotationsTests
    {
        private readonly MetaData _metaData;

        /// <summary>
        /// Get db context and entity meta attributes.
        /// </summary>
        public AnnotationsTests()
        {
            DbContext dbContext = DbContextWithAnnotations.Create();

            _metaData = new MetaData();
            _metaData.LoadFromDbContext(dbContext);
        }

        /// <summary>
        /// Test getting entity meta attributes.
        /// </summary>
        [Fact]
        public void MetaEntityAttributeTest()
        {
            Assert.Equal(1, _metaData.EntityRoot.SubEntities.Count);

            var entity = _metaData.EntityRoot.SubEntities.First();

            Assert.Equal("Test", entity.Name);
            Assert.Equal("Test Description", entity.Description);
            Assert.True(entity.IsEditable);
        }

        /// <summary>
        /// Test getting entity property meta attributes.
        /// </summary>
        [Fact]
        public void MetaEntityAttrAttributeTest()
        {
            var entity = _metaData.EntityRoot.SubEntities.First();
            Assert.Equal(10, entity.Attributes.Count);

            var attr = entity.FindAttributeById("CustomerWithAnnotations.Region");
            Assert.NotNull(attr);
            Assert.False(attr.ShowOnView);
            Assert.True(attr.ShowInLookup);
            Assert.False(attr.IsEditable);
            Assert.Equal("Test", attr.Caption);
        }
    }
}
