using System.Linq;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    // Regression: an enum property mapped to a string via a value converter must still render
    // as a dropdown (ConstListValueEditor), with option ids matching the stored provider values.
    public class EnumConverterMetadataTests
    {
        public enum ServerEngine { SqlServer, Postgres, Mongo }

        public class Server
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

        [Fact]
        public void EnumMappedViaValueConverter_RendersAsDropdownWithProviderValues()
        {
            var meta = new MetaData();
            meta.LoadFromDbContext(EnumConverterDbContext.Create());

            var entity = meta.FindEntity(e => e.ClrType == typeof(Server));
            Assert.NotNull(entity);

            var attr = entity.FindAttribute(a => a.Id.Contains("Engine"));
            Assert.NotNull(attr);

            // Must be a list editor (dropdown), not a plain text editor.
            var editor = Assert.IsType<ConstListValueEditor>(attr.DefaultEditor);

            // Option ids match the provider values stored in the column; labels are the enum names.
            Assert.Equal(new[] { "sqlserver", "postgres", "mongo" }, editor.Values.Select(v => v.Id).ToList());
            Assert.Equal(new[] { "SqlServer", "Postgres", "Mongo" }, editor.Values.Select(v => v.Text).ToList());
        }
    }
}
