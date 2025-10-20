# Codebase Refactoring Plan - Remove Redundancy

## 🔍 **Identified Redundancies**

### 1. **Duplicate Order Controllers** 
- `OrderController.cs` (406 lines) - Advanced with caching, performance monitoring
- `OrderControllerExtended.cs` (193 lines) - Basic CRUD + relationship management

### 2. **Duplicated Cache Management**
- Similar `ClearOrderCache()` and `ClearInventoryCache()` methods
- Brute-force cache invalidation patterns

### 3. **Repeated Performance Logging**
- Identical `LogPerformance()` methods in multiple controllers
- Similar Stopwatch patterns

### 4. **Similar Query Patterns**
- Repeated projection logic across controllers
- Similar caching strategies with slight variations

## 🛠️ **Refactoring Solutions**

### Step 1: Merge Order Controllers

**Action**: Combine both controllers into a single, comprehensive controller

```csharp
[Route("api/[controller]")]
[ApiController]  
[Authorize(Roles = "Manager")]
public class OrderController : BaseController
{
    private readonly LogiTrackContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OrderController> _logger;

    // Merge all functionality:
    // - Basic CRUD operations (from OrderControllerExtended)
    // - Advanced caching & performance monitoring (from OrderController)
    // - Relationship management (AddItemToOrder, RemoveItemFromOrder)
    // - Bulk operations (CreateOrderWithItems)
}
```

### Step 2: Create Base Controller for Shared Logic

```csharp
public abstract class BaseController : ControllerBase
{
    protected readonly ILogger _logger;
    
    protected BaseController(ILogger logger)
    {
        _logger = logger;
    }
    
    protected void LogPerformance(string operation, TimeSpan elapsed, object? additionalInfo = null)
    {
        _logger.LogInformation("Operation: {Operation} | Duration: {ElapsedMs}ms | Additional: {@AdditionalInfo}", 
            operation, elapsed.TotalMilliseconds, additionalInfo);
    }
    
    protected async Task<IActionResult> ExecuteWithPerformanceLogging<T>(
        string operation,
        Func<Task<T>> action,
        Func<T, IActionResult> successResult)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action();
            stopwatch.Stop();
            LogPerformance(operation, stopwatch.Elapsed);
            return successResult(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogPerformance($"{operation}_Error", stopwatch.Elapsed, new { Error = ex.Message });
            throw;
        }
    }
}
```

### Step 3: Create Centralized Cache Management Service

```csharp
public interface ICacheManager
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default);
    Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}

public class CacheManager : ICacheManager
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheManager> _logger;
    private readonly ConcurrentDictionary<string, HashSet<string>> _taggedKeys = new();
    
    // Smart cache invalidation instead of brute force
    public async Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (_taggedKeys.TryGetValue(tag, out var keys))
        {
            foreach (var key in keys)
            {
                _memoryCache.Remove(key);
            }
            _taggedKeys.TryRemove(tag, out _);
            
            _logger.LogInformation("Invalidated {KeyCount} cache entries for tag {Tag}", 
                keys.Count, tag);
        }
    }
    
    public async Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken cancellationToken = default)
    {
        var cacheOptions = new MemoryCacheEntryOptions();
        
        if (options?.SlidingExpiration.HasValue == true)
            cacheOptions.SetSlidingExpiration(options.SlidingExpiration.Value);
            
        if (options?.AbsoluteExpiration.HasValue == true)
            cacheOptions.SetAbsoluteExpiration(options.AbsoluteExpiration.Value);
        
        // Register tags for smart invalidation
        if (options?.Tags?.Any() == true)
        {
            foreach (var tag in options.Tags)
            {
                _taggedKeys.AddOrUpdate(tag,
                    new HashSet<string> { key },
                    (_, existing) => { existing.Add(key); return existing; });
            }
        }
        
        _memoryCache.Set(key, value, cacheOptions);
    }
}

public class CacheOptions
{
    public TimeSpan? SlidingExpiration { get; set; }
    public TimeSpan? AbsoluteExpiration { get; set; }
    public string[]? Tags { get; set; }
}
```

### Step 4: Extract Query Builder Service

```csharp
public class OrderQueryBuilder
{
    private readonly LogiTrackContext _context;
    
    public IQueryable<object> BuildOrderListQuery(int page, int pageSize, bool includeItems)
    {
        var query = _context.Orders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking();

        if (includeItems)
        {
            query = query.Include(o => o.Items);
        }

        return query.Select(order => new 
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
        });
    }
    
    public IQueryable<object> BuildOrderDetailQuery(int id, bool includeItems)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Where(o => o.Id == id);

        if (includeItems)
        {
            query = query.Include(o => o.Items);
        }

        return query.Select(o => new 
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
        });
    }
}
```

### Step 5: Unified Response Format

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public T? Data { get; set; }
    public string? Message { get; set; }
    public object? Metadata { get; set; }
    public PerformanceInfo? Performance { get; set; }
}

public class PerformanceInfo
{
    public double TotalExecutionTimeMs { get; set; }
    public bool CacheHit { get; set; }
    public double? CacheLookupTimeMs { get; set; }
    public double? DatabaseQueryTimeMs { get; set; }
    public double? CacheSetTimeMs { get; set; }
}

public class PaginatedResponse<T> : ApiResponse<IEnumerable<T>>
{
    public PaginationInfo? Pagination { get; set; }
}

public class PaginationInfo
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;
}
```

## 📝 **Implementation Steps**

### Phase 1: Extract Shared Services (1-2 hours)
1. Create `BaseController` class
2. Create `ICacheManager` and `CacheManager` 
3. Create `OrderQueryBuilder` service
4. Create unified response models

### Phase 2: Refactor Controllers (2-3 hours)
1. Update `OrderController` to inherit from `BaseController`
2. Replace direct cache access with `ICacheManager`
3. Replace inline queries with `OrderQueryBuilder`
4. Update `InventoryController` similarly

### Phase 3: Remove Redundant Code (1 hour)
1. Delete `OrderControllerExtended.cs`
2. Move unique functionality to main `OrderController`
3. Remove duplicate cache clearing methods
4. Remove duplicate logging methods

### Phase 4: Update Registration (30 minutes)
```csharp
// In Program.cs
builder.Services.AddScoped<ICacheManager, CacheManager>();
builder.Services.AddScoped<OrderQueryBuilder>();
builder.Services.AddScoped<InventoryQueryBuilder>(); // If created
```

## 📊 **Benefits After Refactoring**

### Code Reduction
- **Before**: 406 + 193 = 599 lines in controllers
- **After**: ~350 lines in single controller + shared services
- **Reduction**: ~40% less code

### Maintainability 
- ✅ Single source of truth for order operations
- ✅ Shared cache management logic
- ✅ Consistent performance logging
- ✅ Unified response formats

### Performance
- ✅ Smart cache invalidation (no more brute force)
- ✅ Reusable query builders
- ✅ Consistent caching strategies

### Testing
- ✅ Easier to test with separated concerns
- ✅ Mockable services
- ✅ Consistent patterns

## 🔧 **Estimated Refactoring Time**
**Total**: 4-6 hours

This refactoring will significantly improve code maintainability while preserving all existing functionality and performance optimizations.