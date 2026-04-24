using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using NDjango.Admin;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.Fixtures
{
    public class TestDbContext : DbContext
    {
        public DbSet<Category> Categories { get; set; }
        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<RestaurantProfile> RestaurantProfiles { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuItemIngredient> MenuItemIngredients { get; set; }
        public DbSet<Gift> Gifts { get; set; }
        public DbSet<ValidatedProduct> ValidatedProducts { get; set; }

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RestaurantProfile>()
                .HasOne(rp => rp.Restaurant)
                .WithOne()
                .HasForeignKey<RestaurantProfile>(rp => rp.RestaurantId);

            modelBuilder.Entity<Gift>()
                .Property(g => g.Price).HasPrecision(10, 2);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.Price).HasPrecision(10, 2);

            modelBuilder.Entity<MenuItemIngredient>(entity =>
            {
                entity.HasKey(e => new { e.MenuItemId, e.IngredientId });

                entity.HasOne(e => e.MenuItem)
                    .WithMany()
                    .HasForeignKey(e => e.MenuItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Ingredient)
                    .WithMany()
                    .HasForeignKey(e => e.IngredientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    public class Category : IAdminSettings<Category>
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Category Name")]
        public string Name { get; set; }

        public string Description { get; set; }

        public PropertyList<Category> SearchFields => new(x => x.Name, x => x.Description);

        public AdminActionList<int> Actions => new AdminActionList<int>()
            .Add("test_action", "Test action for categories",
                handler: async (sp, ids) => { await Task.CompletedTask; return AdminActionResult.Success($"Processed {ids.Count} categories."); })
            .Add("test_error_action", "Test error action",
                handler: async (sp, ids) => { await Task.CompletedTask; return AdminActionResult.Error("Test error message."); });
    }

    public class Restaurant
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string Address { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; }
    }

    public class RestaurantProfile
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Website { get; set; }
        public string Phone { get; set; }

        public int RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; }
    }

    public class Ingredient : IAdminSettings<Ingredient>
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public bool IsAllergen { get; set; }

        public PropertyList<Ingredient> SearchFields => new(x => x.Name);
    }

    public class MenuItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        public int RestaurantId { get; set; }
        public Restaurant Restaurant { get; set; }
    }

    public class MenuItemIngredient
    {
        public int MenuItemId { get; set; }
        public MenuItem MenuItem { get; set; }

        public int IngredientId { get; set; }
        public Ingredient Ingredient { get; set; }
    }

    public class Gift
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public bool IsWrapped { get; set; }
        public Guid TrackingCode { get; set; }
        public decimal Price { get; set; }
        public long Barcode { get; set; }
        public double Weight { get; set; }
        public float Rating { get; set; }
        public short QuantityInStock { get; set; }
        public byte MinAge { get; set; }
        public DateTimeOffset ShippedAt { get; set; }
        public TimeSpan PreparationTime { get; set; }
        public DateOnly ExpirationDate { get; set; }
        public TimeOnly AvailableFrom { get; set; }
        public string Description { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public class ValidatedProduct
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        [Range(1, 1000)]
        public int Quantity { get; set; }

        [RegularExpression(@"^\d{5}$", ErrorMessage = "Must be 5 digits")]
        public string PostalCode { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Url]
        public string Website { get; set; }

        [RegularExpression(@"(?i)^allowed$")]
        public string CaseInsensitive { get; set; }
    }
}
