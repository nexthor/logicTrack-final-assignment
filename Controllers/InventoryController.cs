using Cap1.LogiTrack.DataTransferObjects.Requests;
using Cap1.LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace Cap1.LogiTrack.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize] // Allow any authenticated user to read
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InventoryController> _logger;
    
    public InventoryController(LogiTrackContext context, IMemoryCache cache, ILogger<InventoryController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    private void LogPerformance(string operation, TimeSpan elapsed, object? additionalInfo = null)
    {
        _logger.LogInformation("Operation: {Operation} | Duration: {ElapsedMs}ms | Additional: {@AdditionalInfo}", 
            operation, elapsed.TotalMilliseconds, additionalInfo);
    }

    private void ClearInventoryCache(int? specificItemId = null)
    {
        // Clear paginated results cache
        var cacheKeysToRemove = new List<string>();
        
        // Clear paginated cache keys
        for (int page = 1; page <= 10; page++) // Assuming max 10 pages cached
        {
            for (int pageSize = 1; pageSize <= 100; pageSize += 10)
            {
                cacheKeysToRemove.Add($"InventoryItems_Page_{page}_Size_{pageSize}");
            }
        }

        foreach (var key in cacheKeysToRemove)
        {
            _cache.Remove(key);
        }

        // Clear specific item cache if specified
        if (specificItemId.HasValue)
        {
            _cache.Remove($"InventoryItem_{specificItemId.Value}");
            _cache.Remove($"InventoryItem_{specificItemId.Value}_WithOrder");
        }
        else
        {
            // Clear all individual item caches (brute force approach)
            // In production, consider maintaining a list of cached item IDs
            for (int itemId = 1; itemId <= 1000; itemId++) // Assuming max 1000 items
            {
                _cache.Remove($"InventoryItem_{itemId}");
                _cache.Remove($"InventoryItem_{itemId}_WithOrder");
            }
        }
    }

    [HttpGet]
    [ResponseCache(Duration = 120, VaryByQueryKeys = new[] { "page", "pageSize" })] // 2 minute HTTP cache
    public async Task<IActionResult> GetAllItems([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var totalStopwatch = Stopwatch.StartNew();
        
        if (page <= 0 || pageSize <= 0 || pageSize > 100)
        {
            return BadRequest(new { message = "Invalid pagination parameters. Page must be > 0 and pageSize between 1-100." });
        }

        var cacheKey = $"InventoryItems_Page_{page}_Size_{pageSize}";

        // Measure cache lookup time
        var cacheStopwatch = Stopwatch.StartNew();
        var cacheHit = _cache.TryGetValue(cacheKey, out var cachedResult);
        cacheStopwatch.Stop();

        if (cacheHit)
        {
            totalStopwatch.Stop();
            LogPerformance("GetAllItems_CacheHit", totalStopwatch.Elapsed, new 
            {
                Page = page,
                PageSize = pageSize,
                CacheLookupTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
            });

            var cachedResponse = cachedResult as object;
            return Ok(new 
            {
                data = cachedResponse,
                performance = new
                {
                    totalExecutionTimeMs = totalStopwatch.Elapsed.TotalMilliseconds,
                    cacheHit = true,
                    cacheLookupTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
                }
            });
        }

        // Cache miss - fetch from database
        var countStopwatch = Stopwatch.StartNew();
        var totalItems = await _context.InventoryItems.CountAsync();
        countStopwatch.Stop();

        var queryStopwatch = Stopwatch.StartNew();
        var items = await _context.InventoryItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Order)
            .AsNoTracking()
            .Select(item => new 
            {
                item.Id,
                item.Name,
                item.Quantity,
                item.Location,
                Order = item.Order != null ? new 
                {
                    item.Order.Id,
                    item.Order.CustomerName,
                    item.Order.DatePlaced
                } : null
            })
            .ToListAsync();
        queryStopwatch.Stop();

        var result = new
        {
            data = items,
            pagination = new
            {
                currentPage = page,
                pageSize = pageSize,
                totalItems = totalItems,
                totalPages = (int)Math.Ceiling((double)totalItems / pageSize)
            }
        };

        // Cache the result
        var cacheSetStopwatch = Stopwatch.StartNew();
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(5))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

        _cache.Set(cacheKey, result, cacheEntryOptions);
        cacheSetStopwatch.Stop();

        totalStopwatch.Stop();

        LogPerformance("GetAllItems_CacheMiss", totalStopwatch.Elapsed, new 
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            ResultCount = items.Count,
            CountQueryMs = countStopwatch.Elapsed.TotalMilliseconds,
            MainQueryMs = queryStopwatch.Elapsed.TotalMilliseconds,
            CacheSetMs = cacheSetStopwatch.Elapsed.TotalMilliseconds
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
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "includeOrder" })] // 5 minute HTTP cache
    public async Task<IActionResult> GetItem(int id, [FromQuery] bool includeOrder = false)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Create cache key based on item ID and whether order is included
        var cacheKey = includeOrder ? $"InventoryItem_{id}_WithOrder" : $"InventoryItem_{id}";
        
        // Check cache first
        var cacheStopwatch = Stopwatch.StartNew();
        var cacheHit = _cache.TryGetValue(cacheKey, out var cachedItem);
        cacheStopwatch.Stop();

        if (cacheHit)
        {
            stopwatch.Stop();
            LogPerformance("GetItem_CacheHit", stopwatch.Elapsed, new 
            { 
                ItemId = id, 
                IncludeOrder = includeOrder,
                CacheLookupTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
            });

            return Ok(new 
            {
                data = cachedItem,
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
        var query = _context.InventoryItems
            .AsNoTracking()
            .Where(i => i.Id == id);

        if (includeOrder)
        {
            query = query.Include(i => i.Order);
        }

        var item = await query
            .Select(i => new 
            {
                i.Id,
                i.Name,
                i.Quantity,
                i.Location,
                Order = includeOrder && i.Order != null ? new 
                {
                    i.Order.Id,
                    i.Order.CustomerName,
                    i.Order.DatePlaced
                } : null
            })
            .FirstOrDefaultAsync();
        queryStopwatch.Stop();

        if (item == null)
        {
            stopwatch.Stop();
            LogPerformance("GetItem_NotFound", stopwatch.Elapsed, new 
            { 
                ItemId = id, 
                IncludeOrder = includeOrder,
                QueryTimeMs = queryStopwatch.Elapsed.TotalMilliseconds
            });
            return NotFound(new { message = $"Inventory item with ID {id} not found." });
        }

        // Cache the result
        var cacheSetStopwatch = Stopwatch.StartNew();
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(10))  // Longer for individual items
            .SetAbsoluteExpiration(TimeSpan.FromHours(2))     // 2 hour absolute expiration
            .SetPriority(CacheItemPriority.Normal);

        _cache.Set(cacheKey, item, cacheEntryOptions);
        cacheSetStopwatch.Stop();

        stopwatch.Stop();

        LogPerformance("GetItem_CacheMiss", stopwatch.Elapsed, new 
        { 
            ItemId = id, 
            IncludeOrder = includeOrder,
            HasOrder = item.Order != null,
            QueryTimeMs = queryStopwatch.Elapsed.TotalMilliseconds,
            CacheSetTimeMs = cacheSetStopwatch.Elapsed.TotalMilliseconds
        });

        return Ok(new 
        {
            data = item,
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
    [Authorize(Roles = "Manager,Admin")] // Only managers can create
    public async Task<IActionResult> AddItem([FromBody] CreateInventoryItemRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var createStopwatch = Stopwatch.StartNew();
        var item = new InventoryItem
        {
            Name = request.Name,
            Quantity = request.Quantity,
            Location = request.Location
        };
        createStopwatch.Stop();

        var addStopwatch = Stopwatch.StartNew();
        _context.InventoryItems.Add(item);
        addStopwatch.Stop();

        var saveStopwatch = Stopwatch.StartNew();
        await _context.SaveChangesAsync();
        saveStopwatch.Stop();
        
        // Clear cache after modification
        var cacheStopwatch = Stopwatch.StartNew();
        ClearInventoryCache();
        cacheStopwatch.Stop();

        stopwatch.Stop();

        LogPerformance("AddItem", stopwatch.Elapsed, new 
        {
            ItemName = request.Name,
            Quantity = request.Quantity,
            Location = request.Location,
            CreateTimeMs = createStopwatch.Elapsed.TotalMilliseconds,
            AddTimeMs = addStopwatch.Elapsed.TotalMilliseconds,
            SaveTimeMs = saveStopwatch.Elapsed.TotalMilliseconds,
            CacheClearTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
        });

        var response = new 
        {
            data = item,
            performance = new
            {
                executionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                saveChangesTimeMs = saveStopwatch.Elapsed.TotalMilliseconds,
                cacheClearTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
            }
        };
        
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, response);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Manager,Admin")] // Only managers can update
    public async Task<IActionResult> UpdateItem(int id, [FromBody] UpdateInventoryItemRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var findStopwatch = Stopwatch.StartNew();
        var item = await _context.InventoryItems.FindAsync(id);
        findStopwatch.Stop();

        if (item == null)
        {
            stopwatch.Stop();
            LogPerformance("UpdateItem_NotFound", stopwatch.Elapsed, new { ItemId = id });
            return NotFound(new { message = $"Inventory item with ID {id} not found." });
        }

        var updateStopwatch = Stopwatch.StartNew();
        var fieldsUpdated = new List<string>();
        
        // Update only provided fields
        if (request.Name != null)
        {
            item.Name = request.Name;
            fieldsUpdated.Add("Name");
        }
        if (request.Quantity.HasValue)
        {
            item.Quantity = request.Quantity.Value;
            fieldsUpdated.Add("Quantity");
        }
        if (request.Location != null)
        {
            item.Location = request.Location;
            fieldsUpdated.Add("Location");
        }
        updateStopwatch.Stop();

        var saveStopwatch = Stopwatch.StartNew();
        await _context.SaveChangesAsync();
        saveStopwatch.Stop();
        
        // Clear cache after modification
        var cacheStopwatch = Stopwatch.StartNew();
        ClearInventoryCache(id); // Clear specific item cache
        cacheStopwatch.Stop();

        stopwatch.Stop();

        LogPerformance("UpdateItem", stopwatch.Elapsed, new 
        {
            ItemId = id,
            FieldsUpdated = fieldsUpdated,
            FindTimeMs = findStopwatch.Elapsed.TotalMilliseconds,
            UpdateTimeMs = updateStopwatch.Elapsed.TotalMilliseconds,
            SaveTimeMs = saveStopwatch.Elapsed.TotalMilliseconds,
            CacheClearTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
        });
        
        return Ok(new 
        {
            data = item,
            performance = new
            {
                executionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                fieldsUpdated = fieldsUpdated,
                saveChangesTimeMs = saveStopwatch.Elapsed.TotalMilliseconds,
                cacheClearTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
            }
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager,Admin")] // Only managers can delete
    public async Task<IActionResult> DeleteItem(int id)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var findStopwatch = Stopwatch.StartNew();
        var item = await _context.InventoryItems.FindAsync(id);
        findStopwatch.Stop();

        if (item == null)
        {
            stopwatch.Stop();
            LogPerformance("DeleteItem_NotFound", stopwatch.Elapsed, new { ItemId = id });
            return NotFound(new { message = $"Inventory item with ID {id} not found." });
        }

        var removeStopwatch = Stopwatch.StartNew();
        _context.InventoryItems.Remove(item);
        removeStopwatch.Stop();

        var saveStopwatch = Stopwatch.StartNew();
        await _context.SaveChangesAsync();
        saveStopwatch.Stop();
        
        // Clear cache after modification
        var cacheStopwatch = Stopwatch.StartNew();
        ClearInventoryCache(id); // Clear specific item cache
        cacheStopwatch.Stop();

        stopwatch.Stop();

        LogPerformance("DeleteItem", stopwatch.Elapsed, new 
        {
            ItemId = id,
            ItemName = item.Name,
            FindTimeMs = findStopwatch.Elapsed.TotalMilliseconds,
            RemoveTimeMs = removeStopwatch.Elapsed.TotalMilliseconds,
            SaveTimeMs = saveStopwatch.Elapsed.TotalMilliseconds,
            CacheClearTimeMs = cacheStopwatch.Elapsed.TotalMilliseconds
        });
        
        return NoContent();
    }
}