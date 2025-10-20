using Cap1.LogiTrack;
using Cap1.LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace Cap1.LogiTrack.Examples;

public static class OneToManyExample
{
    public static void DemonstrateRelationship()
    {
        using var context = new LogiTrackContext();
        
        // Ensure database is created
        context.Database.EnsureCreated();
        
        // Clear existing data for demo
        context.InventoryItems.RemoveRange(context.InventoryItems);
        context.Orders.RemoveRange(context.Orders);
        context.SaveChanges();
        
        // Create a new order
        var order = new Order
        {
            CustomerName = "John Doe",
            DatePlaced = DateTime.Now
        };
        
        // Create inventory items
        var item1 = new InventoryItem
        {
            Name = "Laptop",
            Quantity = 1,
            Location = "Electronics Section"
        };
        
        var item2 = new InventoryItem
        {
            Name = "Mouse",
            Quantity = 2,
            Location = "Accessories Section"
        };
        
        var item3 = new InventoryItem
        {
            Name = "Keyboard",
            Quantity = 1,
            Location = "Accessories Section"
        };
        
        // Add items to order using the AddItem method
        order.AddItem(item1);
        order.AddItem(item2);
        order.AddItem(item3);
        
        // Save to database
        context.Orders.Add(order);
        context.SaveChanges();
        
        Console.WriteLine("=== Order and Items Saved ===");
        Console.WriteLine(order.GetOrderSummary());
        Console.WriteLine();
        
        // Demonstrate querying with navigation properties
        Console.WriteLine("=== Items in the Order ===");
        foreach (var item in order.Items)
        {
            Console.WriteLine(item.DisplayInfo());
        }
        Console.WriteLine();
        
        // Query from database with Include to load related data
        var orderFromDb = context.Orders
            .Include(o => o.Items)
            .FirstOrDefault(o => o.CustomerName == "John Doe");
        
        if (orderFromDb != null)
        {
            Console.WriteLine("=== Order Retrieved from Database ===");
            Console.WriteLine(orderFromDb.GetOrderSummary());
            Console.WriteLine("Items in order:");
            foreach (var item in orderFromDb.Items)
            {
                Console.WriteLine($"  - {item.DisplayInfo()}");
            }
            Console.WriteLine();
        }
        
        // Demonstrate querying inventory items and their orders
        var itemsWithOrders = context.InventoryItems
            .Include(i => i.Order)
            .Where(i => i.Order != null)
            .ToList();
        
        Console.WriteLine("=== Items and Their Orders ===");
        foreach (var item in itemsWithOrders)
        {
            Console.WriteLine($"{item.DisplayInfo()} -> Order by {item.Order?.CustomerName}");
        }
        Console.WriteLine();
        
        // Demonstrate removing an item from order
        Console.WriteLine("=== Removing Mouse from Order ===");
        var mouseItem = order.Items.FirstOrDefault(i => i.Name == "Mouse");
        if (mouseItem != null)
        {
            order.RemoveItem(mouseItem);
            context.SaveChanges();
            Console.WriteLine($"Mouse removed. Order now has {order.Items.Count} items.");
            Console.WriteLine($"Mouse OrderId is now: {mouseItem.OrderId ?? 0} (0 means null)");
        }
        Console.WriteLine();
        
        // Create standalone inventory items (not associated with any order)
        var standaloneItem = new InventoryItem
        {
            Name = "Standalone Monitor",
            Quantity = 5,
            Location = "Storage Room"
        };
        
        context.InventoryItems.Add(standaloneItem);
        context.SaveChanges();
        
        // Query all items and show which have orders
        var allItems = context.InventoryItems.Include(i => i.Order).ToList();
        
        Console.WriteLine("=== All Inventory Items ===");
        foreach (var item in allItems)
        {
            var orderInfo = item.Order != null 
                ? $"belongs to order by {item.Order.CustomerName}" 
                : "not assigned to any order";
            Console.WriteLine($"{item.DisplayInfo()} - {orderInfo}");
        }
    }
}