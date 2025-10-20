using Cap1.LogiTrack.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Cap1.LogiTrack.Services;

public class OrderSummaryService
{
    private readonly LogiTrackContext _context;

    public OrderSummaryService(LogiTrackContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Most efficient approach using projection to avoid loading full entities
    /// SQLite-compatible version
    /// </summary>
    public async Task PrintOrderSummariesWithProjectionAsync()
    {
        Console.WriteLine("=== Order Summaries (Projection Method) ===");
        
        // SQLite-compatible projection - get basic order info first
        var orderSummaries = await _context.Orders
            .Select(o => new
            {
                o.Id,
                o.CustomerName,
                o.DatePlaced,
                ItemCount = o.Items.Count(),
                TotalQuantity = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync();

        // Get additional details with separate queries for SQLite compatibility
        foreach (var summary in orderSummaries)
        {
            var orderItems = await _context.InventoryItems
                .Where(i => i.OrderId == summary.Id)
                .Select(i => new { i.Name, i.Location })
                .ToListAsync();

            var locations = orderItems.Select(i => i.Location).Distinct().Where(l => l != null);
            var itemNames = orderItems.Select(i => i.Name).Where(n => n != null);

            Console.WriteLine($"Order #{summary.Id} - {summary.CustomerName}");
            Console.WriteLine($"  Date: {summary.DatePlaced:yyyy-MM-dd}");
            Console.WriteLine($"  Items: {summary.ItemCount} ({summary.TotalQuantity} total quantity)");
            Console.WriteLine($"  Locations: {string.Join(", ", locations)}");
            Console.WriteLine($"  Items: {string.Join(", ", itemNames)}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Efficient batch loading with Include for full object access
    /// </summary>
    public async Task PrintOrderSummariesWithIncludeAsync()
    {
        Console.WriteLine("=== Order Summaries (Include Method) ===");
        
        var orders = await _context.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.DatePlaced)
            .ToListAsync();

        foreach (var order in orders)
        {
            Console.WriteLine(GenerateDetailedSummary(order));
        }
    }

    /// <summary>
    /// Memory efficient streaming approach for large datasets
    /// </summary>
    public async Task PrintOrderSummariesStreamingAsync()
    {
        Console.WriteLine("=== Order Summaries (Streaming Method) ===");
        
        await foreach (var order in _context.Orders
            .Include(o => o.Items)
            .AsAsyncEnumerable())
        {
            Console.WriteLine(GenerateCompactSummary(order));
        }
    }

    /// <summary>
    /// Paginated approach for very large datasets
    /// </summary>
    public async Task PrintOrderSummariesPaginatedAsync(int pageSize = 10)
    {
        Console.WriteLine($"=== Order Summaries (Paginated - {pageSize} per page) ===");
        
        int totalOrders = await _context.Orders.CountAsync();
        int totalPages = (int)Math.Ceiling((double)totalOrders / pageSize);

        for (int page = 0; page < totalPages; page++)
        {
            Console.WriteLine($"\n--- Page {page + 1} of {totalPages} ---");
            
            var orders = await _context.Orders
                .Include(o => o.Items)
                .OrderBy(o => o.Id)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            foreach (var order in orders)
            {
                Console.WriteLine(GenerateCompactSummary(order));
            }
        }
    }

    /// <summary>
    /// Formatted table output for better readability
    /// </summary>
    public async Task PrintOrderSummariesAsTableAsync()
    {
        Console.WriteLine("=== Order Summaries (Table Format) ===");
        
        var summaries = await _context.Orders
            .Select(o => new
            {
                Id = o.Id,
                Customer = o.CustomerName ?? "N/A",
                Date = o.DatePlaced.ToString("yyyy-MM-dd"),
                Items = o.Items.Count(),
                TotalQty = o.Items.Sum(i => i.Quantity)
            })
            .OrderByDescending(o => o.Id)
            .ToListAsync();

        // Print header
        Console.WriteLine($"{"ID",-5} {"Customer",-20} {"Date",-12} {"Items",-6} {"Total Qty",-10}");
        Console.WriteLine(new string('-', 55));

        // Print rows
        foreach (var summary in summaries)
        {
            Console.WriteLine($"{summary.Id,-5} {summary.Customer,-20} {summary.Date,-12} {summary.Items,-6} {summary.TotalQty,-10}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Export summaries to structured format (could be saved to file)
    /// </summary>
    public async Task<List<OrderSummaryDto>> GetOrderSummariesForExportAsync()
    {
        return await _context.Orders
            .Select(o => new OrderSummaryDto
            {
                OrderId = o.Id,
                CustomerName = o.CustomerName ?? "Unknown",
                DatePlaced = o.DatePlaced,
                ItemCount = o.Items.Count(),
                TotalQuantity = o.Items.Sum(i => i.Quantity),
                Items = o.Items.Select(i => new ItemSummaryDto
                {
                    Name = i.Name ?? "Unknown",
                    Quantity = i.Quantity,
                    Location = i.Location ?? "Unknown"
                }).ToList()
            })
            .ToListAsync();
    }

    /// <summary>
    /// Performance-focused summary with minimal data transfer (SQLite optimized)
    /// </summary>
    public async Task PrintQuickOrderStatsAsync()
    {
        Console.WriteLine("=== Quick Order Statistics ===");
        
        // SQLite-compatible approach using separate queries
        var totalOrders = await _context.Orders.CountAsync();
        var totalItems = await _context.InventoryItems.CountAsync(i => i.OrderId != null);
        var totalQuantity = await _context.InventoryItems
            .Where(i => i.OrderId != null)
            .SumAsync(i => i.Quantity);
        
        var orderDates = await _context.Orders
            .Select(o => o.DatePlaced)
            .ToListAsync();
        
        var avgItemsPerOrder = totalOrders > 0 ? (double)totalItems / totalOrders : 0;
        var mostRecentOrder = orderDates.Any() ? orderDates.Max() : DateTime.MinValue;
        var oldestOrder = orderDates.Any() ? orderDates.Min() : DateTime.MinValue;

        Console.WriteLine($"Total Orders: {totalOrders}");
        Console.WriteLine($"Total Items: {totalItems}");
        Console.WriteLine($"Total Quantity: {totalQuantity}");
        Console.WriteLine($"Average Items per Order: {avgItemsPerOrder:F2}");
        if (orderDates.Any())
        {
            Console.WriteLine($"Date Range: {oldestOrder:yyyy-MM-dd} to {mostRecentOrder:yyyy-MM-dd}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Ultra-simple projection method optimized for SQLite
    /// </summary>
    public async Task PrintSimpleOrderSummariesAsync()
    {
        Console.WriteLine("=== Simple Order Summaries (SQLite Optimized) ===");
        
        var basicSummaries = await _context.Orders
            .Select(o => new
            {
                o.Id,
                o.CustomerName,
                o.DatePlaced
            })
            .OrderByDescending(o => o.DatePlaced)
            .ToListAsync();

        foreach (var order in basicSummaries)
        {
            var itemCount = await _context.InventoryItems
                .CountAsync(i => i.OrderId == order.Id);
            
            var totalQuantity = await _context.InventoryItems
                .Where(i => i.OrderId == order.Id)
                .SumAsync(i => i.Quantity);

            Console.WriteLine($"Order #{order.Id}: {order.CustomerName}");
            Console.WriteLine($"  📅 {order.DatePlaced:yyyy-MM-dd}");
            Console.WriteLine($"  📦 {itemCount} items ({totalQuantity} total quantity)");
            Console.WriteLine();
        }
    }

    #region Private Helper Methods

    private static string GenerateDetailedSummary(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📋 Order #{order.Id} - {order.CustomerName}");
        sb.AppendLine($"   📅 Date: {order.DatePlaced:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"   📦 Items ({order.Items.Count}):");
        
        foreach (var item in order.Items)
        {
            sb.AppendLine($"      • {item.Name} (Qty: {item.Quantity}) @ {item.Location}");
        }
        
        var totalQty = order.Items.Sum(i => i.Quantity);
        sb.AppendLine($"   📊 Total Quantity: {totalQty}");
        sb.AppendLine();
        
        return sb.ToString();
    }

    private static string GenerateCompactSummary(Order order)
    {
        var itemCount = order.Items.Count;
        var totalQty = order.Items.Sum(i => i.Quantity);
        return $"Order #{order.Id}: {order.CustomerName} | {order.DatePlaced:MM/dd/yyyy} | {itemCount} items ({totalQty} qty)";
    }

    #endregion
}

// DTOs for structured export
public class OrderSummaryDto
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime DatePlaced { get; set; }
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public List<ItemSummaryDto> Items { get; set; } = new();
}

public class ItemSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Location { get; set; } = string.Empty;
}