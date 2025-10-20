using Cap1.LogiTrack;
using Cap1.LogiTrack.Models;
using Cap1.LogiTrack.Services;

namespace Cap1.LogiTrack.Examples;

public static class OrderSummaryExample
{
    public static async Task DemonstrateEfficientSummaryPrintingAsync()
    {
        using var context = new LogiTrackContext();
        await SeedSampleDataAsync(context);
        
        var summaryService = new OrderSummaryService(context);
        
        Console.WriteLine("🚀 Demonstrating Efficient Order Summary Printing Methods\n");
        
        // Method 1: Simple projection (SQLite optimized)
        await summaryService.PrintSimpleOrderSummariesAsync();
        
        Console.WriteLine("Press any key to continue to advanced projection...");
        Console.ReadKey();
        Console.Clear();
        
        // Method 2: Advanced Projection (Most efficient for large datasets)
        await summaryService.PrintOrderSummariesWithProjectionAsync();
        
        Console.WriteLine("Press any key to continue to Include method...");
        Console.ReadKey();
        Console.Clear();
        
        // Method 3: Include with detailed formatting
        await summaryService.PrintOrderSummariesWithIncludeAsync();
        
        Console.WriteLine("Press any key to continue to table format...");
        Console.ReadKey();
        Console.Clear();
        
        // Method 4: Table format (Great for overview)
        await summaryService.PrintOrderSummariesAsTableAsync();
        
        // Method 5: Quick statistics
        await summaryService.PrintQuickOrderStatsAsync();
        
        Console.WriteLine("Press any key to continue to pagination demo...");
        Console.ReadKey();
        Console.Clear();
        
        // Method 5: Paginated (For very large datasets)
        await summaryService.PrintOrderSummariesPaginatedAsync(pageSize: 2);
        
        Console.WriteLine("Press any key to see export example...");
        Console.ReadKey();
        Console.Clear();
        
        // Method 6: Export format (Structured data)
        await DemonstrateExportFormatAsync(summaryService);
        
        Console.WriteLine("\n✅ All summary printing methods demonstrated!");
    }
    
    private static async Task DemonstrateExportFormatAsync(OrderSummaryService service)
    {
        Console.WriteLine("=== Export Format Example ===");
        
        var exportData = await service.GetOrderSummariesForExportAsync();
        
        // Demonstrate how this could be used for JSON export, CSV, etc.
        foreach (var summary in exportData.Take(2)) // Show first 2 for demo
        {
            Console.WriteLine($"Export Data for Order {summary.OrderId}:");
            Console.WriteLine($"  Customer: {summary.CustomerName}");
            Console.WriteLine($"  Date: {summary.DatePlaced:yyyy-MM-dd}");
            Console.WriteLine($"  Total Items: {summary.ItemCount}");
            Console.WriteLine($"  Total Quantity: {summary.TotalQuantity}");
            Console.WriteLine("  Item Details:");
            
            foreach (var item in summary.Items)
            {
                Console.WriteLine($"    - {item.Name}: {item.Quantity} @ {item.Location}");
            }
            Console.WriteLine();
        }
        
        Console.WriteLine($"💾 Total {exportData.Count} orders ready for export");
    }
    
    private static async Task SeedSampleDataAsync(LogiTrackContext context)
    {
        // Clear existing data
        context.InventoryItems.RemoveRange(context.InventoryItems);
        context.Orders.RemoveRange(context.Orders);
        await context.SaveChangesAsync();
        
        // Create sample orders with items
        var orders = new[]
        {
            new Order
            {
                CustomerName = "Alice Johnson",
                DatePlaced = DateTime.Now.AddDays(-5)
            },
            new Order
            {
                CustomerName = "Bob Smith",
                DatePlaced = DateTime.Now.AddDays(-3)
            },
            new Order
            {
                CustomerName = "Carol Williams",
                DatePlaced = DateTime.Now.AddDays(-1)
            },
            new Order
            {
                CustomerName = "David Brown",
                DatePlaced = DateTime.Now
            }
        };
        
        // Add items to orders
        orders[0].AddItem(new InventoryItem { Name = "Laptop Pro", Quantity = 1, Location = "Electronics" });
        orders[0].AddItem(new InventoryItem { Name = "Wireless Mouse", Quantity = 2, Location = "Accessories" });
        orders[0].AddItem(new InventoryItem { Name = "USB-C Hub", Quantity = 1, Location = "Accessories" });
        
        orders[1].AddItem(new InventoryItem { Name = "Gaming Chair", Quantity = 1, Location = "Furniture" });
        orders[1].AddItem(new InventoryItem { Name = "Desk Lamp", Quantity = 1, Location = "Lighting" });
        
        orders[2].AddItem(new InventoryItem { Name = "Smartphone", Quantity = 1, Location = "Electronics" });
        orders[2].AddItem(new InventoryItem { Name = "Phone Case", Quantity = 1, Location = "Accessories" });
        orders[2].AddItem(new InventoryItem { Name = "Screen Protector", Quantity = 2, Location = "Accessories" });
        orders[2].AddItem(new InventoryItem { Name = "Wireless Charger", Quantity = 1, Location = "Electronics" });
        
        orders[3].AddItem(new InventoryItem { Name = "Bluetooth Headphones", Quantity = 1, Location = "Audio" });
        
        context.Orders.AddRange(orders);
        await context.SaveChangesAsync();
        
        Console.WriteLine($"✅ Seeded {orders.Length} sample orders with items\n");
    }
}