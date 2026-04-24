using System;
using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;

namespace NDjango.Admin.EntityFrameworkCore.Relational.Tests
{
    public class DbContextWithValidation : DbContext
    {
        public DbContextWithValidation(DbContextOptions options)
            : base(options)
        { }

        public DbSet<ValidationEntity> ValidationEntities { get; set; }

        public static DbContextWithValidation Create()
        {
            return new DbContextWithValidation(new DbContextOptionsBuilder()
                .UseSqlite("Data Source = :memory:")
                .Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ValidationEntity>(b =>
            {
                b.Property(e => e.FluentMaxLen).HasMaxLength(25);
                b.Property(e => e.Price).HasPrecision(10, 2);
            });
        }
    }

    public class ValidationEntity
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string MaxLengthString { get; set; }

        public string FluentMaxLen { get; set; }

        [StringLength(maximumLength: 100, MinimumLength = 3)]
        public string StringLengthField { get; set; }

        [MinLength(5)]
        public string MinLengthField { get; set; }

        [MinLength(0)]
        public string ZeroMinLengthField { get; set; }

        [Range(1, 1000)]
        public int IntRange { get; set; }

        [Range(typeof(DateTime), "2020-01-01", "2030-12-31")]
        public DateTime DateRange { get; set; }

        [RegularExpression(@"^\d{5}$", ErrorMessage = "Must be 5 digits")]
        public string PostalCode { get; set; }

        [RegularExpression(@"(?i)^hello$")]
        public string CaseInsensitivePattern { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Url]
        public string Website { get; set; }

        [Phone]
        public string Phone { get; set; }

        public decimal Price { get; set; }
    }
}
