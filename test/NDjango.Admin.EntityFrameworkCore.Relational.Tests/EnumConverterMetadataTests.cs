using System.Linq;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    // Regression: an enum property mapped to a string via a value converter must still render
    // as a dropdown (ConstListValueEditor), with option ids matching the stored provider values.
    public class EnumConverterMetadataTests
    {
        private static readonly string[] ProviderValues = { "sqlserver", "postgres", "mongo" };
        private static readonly string[] EnumNames = { "SqlServer", "Postgres", "Mongo" };
        private static readonly string[] UnderlyingValues = { "0", "1", "2" };

        public enum ServerEngine { SqlServer, Postgres, Mongo }

        public class Server
        {
            public int Id { get; set; }
            public ServerEngine Engine { get; set; }
        }

        public class PlainServer
        {
            public int Id { get; set; }
            public ServerEngine Engine { get; set; }
        }

        private class EnumConverterDbContext : DbContext
        {
            public EnumConverterDbContext(DbContextOptions options) : base(options) { }

            public DbSet<Server> Servers { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Server>().Property(s => s.Engine)
                    .HasConversion(
                        v => v == ServerEngine.SqlServer ? "sqlserver" : v == ServerEngine.Postgres ? "postgres" : "mongo",
                        v => v == "sqlserver" ? ServerEngine.SqlServer : v == "postgres" ? ServerEngine.Postgres : ServerEngine.Mongo)
                    .HasMaxLength(20);
            }

            public static EnumConverterDbContext Create() =>
                new EnumConverterDbContext(new DbContextOptionsBuilder()
                    .UseSqlite("Data Source=:memory:")
                    .Options);
        }

        private class PlainEnumDbContext : DbContext
        {
            public PlainEnumDbContext(DbContextOptions options) : base(options) { }

            public DbSet<PlainServer> Servers { get; set; }

            public static PlainEnumDbContext Create() =>
                new PlainEnumDbContext(new DbContextOptionsBuilder()
                    .UseSqlite("Data Source=:memory:")
                    .Options);
        }

        [Fact]
        public void EnumMappedViaValueConverter_RendersAsDropdownWithProviderValues()
        {
            // Arrange
            var meta = new MetaData();
            meta.LoadFromDbContext(EnumConverterDbContext.Create());

            // Act
            var entity = meta.FindEntity(e => e.ClrType == typeof(Server));
            var attr = entity?.FindAttribute(a => a.Id.Contains("Engine"));

            // Assert
            Assert.NotNull(entity);
            Assert.NotNull(attr);
            // Must be a list editor (dropdown), not a plain text editor.
            var editor = Assert.IsType<ConstListValueEditor>(attr.DefaultEditor);
            // Option ids match the provider values stored in the column; labels are the enum names.
            Assert.Equal(ProviderValues, editor.Values.Select(v => v.Id).ToList());
            Assert.Equal(EnumNames, editor.Values.Select(v => v.Text).ToList());
            // A converter already stores a readable value, so no extra display format is needed.
            Assert.Null(attr.DisplayFormat);
            // DataType reflects the provider (column) type so validation accepts the posted string
            // value (e.g. "sqlserver") instead of demanding an integer.
            Assert.Equal(DataType.String, attr.DataType);
        }

        [Fact]
        public void EnumWithoutConverter_RendersAsDropdownWithUnderlyingValues()
        {
            // Arrange
            var meta = new MetaData();
            meta.LoadFromDbContext(PlainEnumDbContext.Create());

            // Act
            var entity = meta.FindEntity(e => e.ClrType == typeof(PlainServer));
            var attr = entity?.FindAttribute(a => a.Id.Contains("Engine"));

            // Assert
            Assert.NotNull(entity);
            Assert.NotNull(attr);
            // Must be a list editor (dropdown), not a plain text editor.
            var editor = Assert.IsType<ConstListValueEditor>(attr.DefaultEditor);
            // Without a converter the column stores the enum's underlying number.
            Assert.Equal(UnderlyingValues, editor.Values.Select(v => v.Id).ToList());
            Assert.Equal(EnumNames, editor.Values.Select(v => v.Text).ToList());
            // The int->name display map is required so lists show readable text.
            Assert.NotNull(attr.DisplayFormat);
            // Without a converter the column stores the underlying int, so the field is integer-typed.
            Assert.Equal(DataType.Int32, attr.DataType);
        }
    }
}
