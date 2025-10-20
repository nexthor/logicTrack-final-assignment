namespace Cap1.LogiTrack.Models;

public class Order
{
    public int Id { get; set; }
    public string? CustomerName { get; set; }
    public DateTime DatePlaced { get; set; }
    
    // Navigation property for one-to-many relationship
    public List<InventoryItem> Items { get; set; } = new List<InventoryItem>();

    /// <summary>
    /// Adds an item to the order with validation and duplicate checking
    /// </summary>
    /// <param name="item">The inventory item to add</param>
    /// <returns>True if item was added successfully, false otherwise</returns>
    public bool AddItem(InventoryItem item)
    {
        // Input validation
        if (item == null) return false;
        
        // Initialize collection if needed
        Items ??= new List<InventoryItem>();

        // Check for duplicates (early termination with Any())
        if (Items.Any(i => i.Id == item.Id)) return false;

        // Business rule: Don't steal items from other orders
        if (item.OrderId.HasValue && item.OrderId != Id) return false;

        // Add item and establish relationship
        Items.Add(item);
        item.OrderId = Id;
        item.Order = this;
        
        return true;
    }

    /// <summary>
    /// Removes an item from the order
    /// </summary>
    /// <param name="item">The inventory item to remove</param>
    /// <returns>True if item was removed, false if not found</returns>
    public bool RemoveItem(InventoryItem item)
    {
        if (item == null) return false;
        
        var removed = Items.Remove(item);
        if (removed)
        {
            item.OrderId = null;
            item.Order = null;
        }
        
        return removed;
    }

    /// <summary>
    /// Adds multiple items efficiently in one operation
    /// </summary>
    /// <param name="items">Items to add</param>
    /// <returns>Number of items successfully added</returns>
    public int AddItems(IEnumerable<InventoryItem> items)
    {
        if (items == null) return 0;

        Items ??= new List<InventoryItem>();
        var itemList = items.ToList();
        
        // Pre-allocate capacity for better performance
        if (Items.Capacity < Items.Count + itemList.Count)
            Items.Capacity = Items.Count + itemList.Count;

        var addedCount = 0;
        var existingIds = new HashSet<int>(Items.Select(i => i.Id));
        
        foreach (var item in itemList)
        {
            if (item == null || existingIds.Contains(item.Id)) continue;
            if (item.OrderId.HasValue && item.OrderId != Id) continue;

            Items.Add(item);
            item.OrderId = Id;
            item.Order = this;
            existingIds.Add(item.Id);
            addedCount++;
        }

        return addedCount;
    }

    public string GetOrderSummary()
    {
        return $"Order for {CustomerName} placed on {DatePlaced.ToShortDateString()} with {Items.Count} items.";
    }
}