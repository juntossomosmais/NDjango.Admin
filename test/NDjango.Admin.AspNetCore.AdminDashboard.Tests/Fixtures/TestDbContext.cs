using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RestaurantProfile>()
                .HasOne(rp => rp.Restaurant)
                .WithOne()
                .HasForeignKey<RestaurantProfile>(rp => rp.RestaurantId);
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
}
