using Cap1.LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace Cap1.LogiTrack.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Manager")]
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly ILogger<OrderController> _logger;
    private readonly IMemoryCache _cache;

    public OrderController(LogiTrackContext context, ILogger<OrderController> logger, IMemoryCache cache)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
    }

    private void LogPerformance(string operation, TimeSpan elapsed, object? additionalInfo = null)
    {
        _logger.LogInformation("Operation: {Operation} | Duration: {ElapsedMs}ms | Additional: {@AdditionalInfo}", 
            operation, elapsed.TotalMilliseconds, additionalInfo);
    }

    private void ClearOrderCache(int? specificOrderId = null)
    {
        // Clear paginated results cache
        var cacheKeysToRemove = new List<string>();
        
        // Clear paginated cache keys
        for (int page = 1; page <= 10; page++)
        {
            for (int pageSize = 1; pageSize <= 100; pageSize += 10)
            {
                cacheKeysToRemove.Add($"Orders_Page_{page}_Size_{pageSize}_IncludeItems_true");
                cacheKeysToRemove.Add($"Orders_Page_{page}_Size_{pageSize}_IncludeItems_false");
            }
        }

        foreach (var key in cacheKeysToRemove)
        {
            _cache.Remove(key);
        }

        // Clear specific order cache if specified
        if (specificOrderId.HasValue)
        {
            _cache.Remove($"Order_{specificOrderId.Value}");
            _cache.Remove($"Order_{specificOrderId.Value}_WithItems");
        }
        else
        {
            // Clear all individual order caches (brute force approach)
            for (int orderId = 1; orderId <= 1000; orderId++)
            {
                _cache.Remove($"Order_{orderId}");
                _cache.Remove($"Order_{orderId}_WithItems");
            }
        }
    }

    [HttpGet]
    [ResponseCache(Duration = 180, VaryByQueryKeys = new[] { "page", "pageSize", "includeItems" })] // 3 minute HTTP cache
    public async Task<IActionResult> GetAllOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] bool includeItems = false)
    {
        var totalStopwatch = Stopwatch.StartNew();
        
        if (page <= 0 || pageSize <= 0 || pageSize > 100)
        {
            return BadRequest(new { message = "Invalid pagination parameters. Page must be > 0 and pageSize between 1-100." });
        }

        // Create cache key
        var cacheKey = $"Orders_Page_{page}_Size_{pageSize}_IncludeItems_{includeItems}";

        // Check cache first
        var cacheStopwatch = Stopwatch.StartNew();
        var cacheHit = _cache.TryGetValue(cacheKey, out var cachedResult);
        cacheStopwatch.Stop();

        if (cacheHit)
        {
            totalStopwatch.Stop();
            LogPerformance("GetAllOrders_CacheHit", totalStopwatch.Elapsed, new 
            {
                Page = page,
                PageSize = pageSize,
                IncludeItems = includeItems,
                CacheLookupTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
            });

            return Ok(new 
            {
                data = cachedResult,
                performance = new
                {
                    totalExecutionTimeMs = totalStopwatch.Elapsed.TotalMilliseconds,
                    cacheHit = true,
                    cacheLookupTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
                }
            });
        }

        // Cache miss - fetch from database
        // Measure count query performance
        var countStopwatch = Stopwatch.StartNew();
        var totalOrders = await _context.Orders.CountAsync();
        countStopwatch.Stop();

        // Measure main query performance
        var queryStopwatch = Stopwatch.StartNew();
        var query = _context.Orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking();

        if (includeItems)
        {
            query = query.Include(o => o.Items);
        }

        var orders = await query
            .Select(order => new 
            {
                order.Id,
                order.CustomerName,
                order.DatePlaced,
                ItemsCount = order.Items.Count(),
                Items = includeItems ? order.Items.Select(item => new 
                {
                    item.Id,
                    item.Name,
                    item.Quantity,
                    item.Location
                }) : null
            })
            .ToListAsync();
        queryStopwatch.Stop();

        var result = new
        {
            data = orders,
            pagination = new
            {
                currentPage = page,
                pageSize = pageSize,
                totalItems = totalOrders,
                totalPages = (int)Math.Ceiling((double)totalOrders / pageSize)
            }
        };

        // Cache the result
        var cacheSetStopwatch = Stopwatch.StartNew();
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(3))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(15))
            .SetPriority(CacheItemPriority.Normal);

        _cache.Set(cacheKey, result, cacheEntryOptions);
        cacheSetStopwatch.Stop();

        totalStopwatch.Stop();

        // Log performance metrics
        LogPerformance("GetAllOrders_CacheMiss", totalStopwatch.Elapsed, new 
        {
            Page = page,
            PageSize = pageSize,
            IncludeItems = includeItems,
            TotalOrders = totalOrders,
            CountQueryMs = countStopwatch.Elapsed.TotalMilliseconds,
            MainQueryMs = queryStopwatch.Elapsed.TotalMilliseconds,
            CacheSetMs = cacheSetStopwatch.Elapsed.TotalMilliseconds,
            ResultCount = orders.Count
        });

        return Ok(new
        {
            data = result.data,
            pagination = result.pagination,
            performance = new
            {
                totalExecutionTimeMs = totalStopwatch.Elapsed.TotalMilliseconds,
                cacheHit = false,
                countQueryTimeMs = countStopwatch.Elapsed.TotalMilliseconds,
                mainQueryTimeMs = queryStopwatch.Elapsed.TotalMilliseconds,
                cacheSetTimeMs = cacheSetStopwatch.Elapsed.TotalMilliseconds
            }
        });
    }

    [HttpGet("{id}")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "includeItems" })] // 5 minute HTTP cache
    public async Task<IActionResult> GetOrderById(int id, [FromQuery] bool includeItems = true)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Create cache key based on order ID and whether items are included
        var cacheKey = includeItems ? $"Order_{id}_WithItems" : $"Order_{id}";
        
        // Check cache first
        var cacheStopwatch = Stopwatch.StartNew();
        var cacheHit = _cache.TryGetValue(cacheKey, out var cachedOrder);
        cacheStopwatch.Stop();

        if (cacheHit)
        {
            stopwatch.Stop();
            LogPerformance("GetOrderById_CacheHit", stopwatch.Elapsed, new 
            { 
                OrderId = id, 
                IncludeItems = includeItems,
                CacheLookupTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
            });

            return Ok(new 
            {
                data = cachedOrder,
                performance = new
                {
                    executionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    cacheHit = true,
                    cacheLookupTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
                }
            });
        }

        // Cache miss - fetch from database
        var queryStopwatch = Stopwatch.StartNew();
        var query = _context.Orders
            .AsNoTracking()
            .Where(o => o.Id == id);

        if (includeItems)
        {
            query = query.Include(o => o.Items);
        }

        var order = await query
            .Select(o => new 
            {
                o.Id,
                o.CustomerName,
                o.DatePlaced,
                ItemsCount = o.Items.Count(),
                Items = includeItems ? o.Items.Select(item => new 
                {
                    item.Id,
                    item.Name,
                    item.Quantity,
                    item.Location
                }) : null
            })
            .FirstOrDefaultAsync();
        queryStopwatch.Stop();

        if (order == null)
        {
            stopwatch.Stop();
            LogPerformance("GetOrderById_NotFound", stopwatch.Elapsed, new 
            { 
                OrderId = id, 
                IncludeItems = includeItems,
                QueryTimeMs = queryStopwatch.Elapsed.TotalMilliseconds
            });
            return NotFound(new { message = $"Order with ID {id} not found." });
        }

        // Cache the result
        var cacheSetStopwatch = Stopwatch.StartNew();
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(15))  // Longer for individual orders
            .SetAbsoluteExpiration(TimeSpan.FromHours(4))     // 4 hour absolute expiration
            .SetPriority(CacheItemPriority.Normal);

        _cache.Set(cacheKey, order, cacheEntryOptions);
        cacheSetStopwatch.Stop();

        stopwatch.Stop();

        LogPerformance("GetOrderById_CacheMiss", stopwatch.Elapsed, new 
        { 
            OrderId = id, 
            IncludeItems = includeItems,
            ItemsCount = order.ItemsCount,
            QueryTimeMs = queryStopwatch.Elapsed.TotalMilliseconds,
            CacheSetTimeMs = cacheSetStopwatch.Elapsed.TotalMilliseconds
        });

        return Ok(new 
        {
            data = order,
            performance = new
            {
                executionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                cacheHit = false,
                queryTimeMs = queryStopwatch.Elapsed.TotalMilliseconds,
                cacheSetTimeMs = cacheSetStopwatch.Elapsed.TotalMilliseconds
            }
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] Order order)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var addStopwatch = Stopwatch.StartNew();
        _context.Orders.Add(order);
        addStopwatch.Stop();

        var saveStopwatch = Stopwatch.StartNew();
        await _context.SaveChangesAsync();
        saveStopwatch.Stop();
        
        // Clear cache after creating new order
        var cacheStopwatch = Stopwatch.StartNew();
        ClearOrderCache();
        cacheStopwatch.Stop();
        
        stopwatch.Stop();

        // Log performance metrics
        LogPerformance("CreateOrder", stopwatch.Elapsed, new 
        {
            CustomerName = order.CustomerName,
            ItemsCount = order.Items.Count,
            AddTimeMs = addStopwatch.Elapsed.TotalMilliseconds,
            SaveTimeMs = saveStopwatch.Elapsed.TotalMilliseconds,
            CacheClearTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
        });

        // Return simplified order data instead of full entity
        var createdOrder = new 
        {
            order.Id,
            order.CustomerName,
            order.DatePlaced,
            ItemsCount = order.Items.Count,
            performance = new
            {
                executionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                saveChangesTimeMs = saveStopwatch.Elapsed.TotalMilliseconds,
                cacheClearTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
            }
        };

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, createdOrder);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var findStopwatch = Stopwatch.StartNew();
        var order = await _context.Orders.FindAsync(id);
        findStopwatch.Stop();

        if (order == null)
        {
            stopwatch.Stop();
            LogPerformance("DeleteOrder_NotFound", stopwatch.Elapsed, new { OrderId = id });
            return NotFound(new { message = $"Order with ID {id} not found." });
        }

        var removeStopwatch = Stopwatch.StartNew();
        _context.Orders.Remove(order);
        removeStopwatch.Stop();

        var saveStopwatch = Stopwatch.StartNew();
        await _context.SaveChangesAsync();
        saveStopwatch.Stop();
        
        // Clear cache after deletion
        var cacheStopwatch = Stopwatch.StartNew();
        ClearOrderCache(id); // Clear specific order cache
        cacheStopwatch.Stop();
        
        stopwatch.Stop();

        LogPerformance("DeleteOrder", stopwatch.Elapsed, new 
        {
            OrderId = id,
            CustomerName = order.CustomerName,
            FindTimeMs = findStopwatch.Elapsed.TotalMilliseconds,
            RemoveTimeMs = removeStopwatch.Elapsed.TotalMilliseconds,
            SaveTimeMs = saveStopwatch.Elapsed.TotalMilliseconds,
            CacheClearTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
        });

        return NoContent();
    }
}