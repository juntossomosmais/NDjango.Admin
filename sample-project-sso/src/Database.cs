using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<Restaurant> Restaurants { get; set; }
    public DbSet<RestaurantProfile> RestaurantProfiles { get; set; }
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public static AppDbContext CreateContext(string connectionString,
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = null)
    {
        if (optionsBuilder is null)
            optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        optionsBuilder.UseSqlServer(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(StandardEntity).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(StandardEntity.CreatedAt))
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(StandardEntity.UpdatedAt))
                .HasDefaultValueSql("GETUTCDATE()");
        }

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.Property(r => r.Name).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Address).IsRequired().HasMaxLength(300);
            entity.Property(r => r.Phone).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<RestaurantProfile>(entity =>
        {
            entity.Property(rp => rp.Website).HasMaxLength(200);
            entity.Property(rp => rp.OpeningHours).IsRequired().HasMaxLength(100);

            entity.HasOne(rp => rp.Restaurant)
                .WithOne(r => r.Profile)
                .HasForeignKey<RestaurantProfile>(rp => rp.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.Property(i => i.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(i => i.Name).IsUnique();
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.Property(mi => mi.Name).IsRequired().HasMaxLength(200);
            entity.Property(mi => mi.Price).HasPrecision(10, 2);

            entity.HasOne(mi => mi.Restaurant)
                .WithMany(r => r.MenuItems)
                .HasForeignKey(mi => mi.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(mi => mi.Ingredients)
                .WithMany(i => i.MenuItems);

            entity.HasIndex(mi => new { mi.RestaurantId, mi.Name });
        });
    }

    public override int SaveChanges()
    {
        AutomaticallyAddCreatedAndUpdatedAt();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AutomaticallyAddCreatedAndUpdatedAt();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void AutomaticallyAddCreatedAndUpdatedAt()
    {
        var entitiesOnDbContext = ChangeTracker.Entries<StandardEntity>();

        if (entitiesOnDbContext is null)
            return;

        foreach (var item in entitiesOnDbContext.Where(t => t.State == EntityState.Added))
        {
            item.Entity.CreatedAt = DateTime.Now.ToUniversalTime();
            item.Entity.UpdatedAt = DateTime.Now.ToUniversalTime();
        }

        foreach (var item in entitiesOnDbContext.Where(t => t.State == EntityState.Modified))
        {
            item.Entity.UpdatedAt = DateTime.Now.ToUniversalTime();
        }
    }
}
