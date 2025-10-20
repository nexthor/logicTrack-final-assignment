using Cap1.LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cap1.LogiTrack.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderControllerExtended : ControllerBase
{
    private readonly LogiTrackContext _context;

    public OrderControllerExtended(LogiTrackContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _context.Orders.Include(o => o.Items).ToListAsync();
        return Ok(orders);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Add an existing inventory item to an order
    /// </summary>
    [HttpPost("{orderId}/items/{itemId}")]
    public async Task<IActionResult> AddItemToOrder(int orderId, int itemId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        if (order == null)
            return NotFound($"Order {orderId} not found");

        var item = await _context.InventoryItems.FindAsync(itemId);
        if (item == null)
            return NotFound($"Inventory item {itemId} not found");

        // Check if item is already assigned to another order
        if (item.OrderId.HasValue && item.OrderId != orderId)
            return BadRequest($"Item {itemId} is already assigned to order {item.OrderId}");

        // Check if item is already in this order
        if (order.Items.Any(i => i.Id == itemId))
            return BadRequest($"Item {itemId} is already in order {orderId}");

        // Add item to order
        item.OrderId = orderId;
        item.Order = order;
        order.Items.Add(item);

        await _context.SaveChangesAsync();
        
        return Ok(new { message = $"Item {itemId} added to order {orderId}" });
    }

    /// <summary>
    /// Remove an inventory item from an order
    /// </summary>
    [HttpDelete("{orderId}/items/{itemId}")]
    public async Task<IActionResult> RemoveItemFromOrder(int orderId, int itemId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        if (order == null)
            return NotFound($"Order {orderId} not found");

        var item = order.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null)
            return NotFound($"Item {itemId} not found in order {orderId}");

        // Remove item from order
        item.OrderId = null;
        item.Order = null;
        order.Items.Remove(item);

        await _context.SaveChangesAsync();
        
        return Ok(new { message = $"Item {itemId} removed from order {orderId}" });
    }

    /// <summary>
    /// Get all items for a specific order
    /// </summary>
    [HttpGet("{orderId}/items")]
    public async Task<IActionResult> GetOrderItems(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        if (order == null)
            return NotFound($"Order {orderId} not found");

        return Ok(order.Items);
    }

    /// <summary>
    /// Create an order with items in a single transaction
    /// </summary>
    [HttpPost("with-items")]
    public async Task<IActionResult> CreateOrderWithItems([FromBody] CreateOrderWithItemsDto dto)
    {
        var order = new Order
        {
            CustomerName = dto.CustomerName,
            DatePlaced = dto.DatePlaced
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(); // Save to get the order ID

        // Add items if provided
        if (dto.Items != null && dto.Items.Any())
        {
            foreach (var itemDto in dto.Items)
            {
                var item = new InventoryItem
                {
                    Name = itemDto.Name,
                    Quantity = itemDto.Quantity,
                    Location = itemDto.Location,
                    OrderId = order.Id,
                    Order = order
                };
                
                _context.InventoryItems.Add(item);
                order.Items.Add(item);
            }
            
            await _context.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }
}

// DTO classes
public class CreateOrderWithItemsDto
{
    public string? CustomerName { get; set; }
    public DateTime DatePlaced { get; set; }
    public List<CreateInventoryItemDto>? Items { get; set; }
}

public class CreateInventoryItemDto
{
    public string? Name { get; set; }
    public int Quantity { get; set; }
    public string? Location { get; set; }
}