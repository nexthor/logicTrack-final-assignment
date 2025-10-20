namespace Cap1.LogiTrack.Models;

public class InventoryItem
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Quantity { get; set; }
    public string? Location { get; set; }

    // Foreign key property
    public int? OrderId { get; set; }

    // Navigation property
    public Order? Order { get; set; }

    public string DisplayInfo()
    {
        return $"Item: {Name}, Quantity: {Quantity}, Location: {Location}";
    }
}