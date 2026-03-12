using NDjango.Admin;

public abstract class StandardEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Category : StandardEntity, IAdminSettings<Category>
{
    public string Name { get; set; }
    public string Description { get; set; } = "";

    public PropertyList<Category> SearchFields => new(x => x.Name, x => x.Description);
}

public class Restaurant : StandardEntity, IAdminSettings<Restaurant>
{
    public string Name { get; set; }
    public string Address { get; set; }
    public string Phone { get; set; }

    public RestaurantProfile Profile { get; set; }
    public IList<MenuItem> MenuItems { get; set; }

    public PropertyList<Restaurant> SearchFields => new(x => x.Name);
}

public class RestaurantProfile : StandardEntity
{
    public int RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; }

    public string Website { get; set; } = "";
    public string OpeningHours { get; set; }
    public int Capacity { get; set; }
}

public class Ingredient : StandardEntity
{
    public string Name { get; set; }
    public bool IsAllergen { get; set; }

    public IList<MenuItem> MenuItems { get; set; }
}

public class MenuItem : StandardEntity
{
    public int RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; }

    public string Name { get; set; }
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;

    public IList<Ingredient> Ingredients { get; set; }
}
