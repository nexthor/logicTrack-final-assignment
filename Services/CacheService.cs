using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Cap1.LogiTrack.Services;

/// <summary>
/// Abstraction layer for caching that can work with both in-memory and distributed caches
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemovePatternAsync(string pattern, CancellationToken cancellationToken = default);
}

/// <summary>
/// Memory cache implementation of ICacheService
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheService> _logger;

    public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var result = _memoryCache.TryGetValue(key, out var value) ? (T?)value : default(T);
        return Task.FromResult(result);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();
        
        if (slidingExpiration.HasValue)
            options.SetSlidingExpiration(slidingExpiration.Value);
            
        if (absoluteExpiration.HasValue)
            options.SetAbsoluteExpiration(absoluteExpiration.Value);
            
        options.SetPriority(CacheItemPriority.Normal);

        _memoryCache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemovePatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Note: Memory cache doesn't support pattern-based removal efficiently
        // This is a simplified implementation for demonstration
        _logger.LogWarning("Pattern-based cache removal not efficiently supported in MemoryCache: {Pattern}", pattern);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Distributed cache implementation of ICacheService (Redis, SQL Server, etc.)
/// </summary>
public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<DistributedCacheService> _logger;

    public DistributedCacheService(IDistributedCache distributedCache, ILogger<DistributedCacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var jsonData = await _distributedCache.GetStringAsync(key, cancellationToken);
        
        if (string.IsNullOrEmpty(jsonData))
            return default(T);

        try
        {
            return JsonSerializer.Deserialize<T>(jsonData);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize cached value for key: {Key}", key);
            return default(T);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions();
        
        if (slidingExpiration.HasValue)
            options.SetSlidingExpiration(slidingExpiration.Value);
            
        if (absoluteExpiration.HasValue)
            options.SetAbsoluteExpiration(absoluteExpiration.Value);

        try
        {
            var jsonData = JsonSerializer.Serialize(value);
            await _distributedCache.SetStringAsync(key, jsonData, options, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize value for caching with key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _distributedCache.RemoveAsync(key, cancellationToken);
    }

    public Task RemovePatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Note: Most distributed caches don't support pattern-based removal
        // This would require custom implementation based on the specific cache provider
        _logger.LogWarning("Pattern-based cache removal not supported in basic distributed cache: {Pattern}", pattern);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Hybrid cache service that uses both local memory cache and distributed cache
/// </summary>
public class HybridCacheService : ICacheService
{
    private readonly IMemoryCache _localCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<HybridCacheService> _logger;
    private readonly TimeSpan _localCacheDuration = TimeSpan.FromMinutes(2); // Short local cache

    public HybridCacheService(IMemoryCache localCache, IDistributedCache distributedCache, ILogger<HybridCacheService> logger)
    {
        _localCache = localCache;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        // Try local cache first (fastest)
        if (_localCache.TryGetValue(key, out var localValue))
        {
            return (T?)localValue;
        }

        // Try distributed cache
        var jsonData = await _distributedCache.GetStringAsync(key, cancellationToken);
        
        if (string.IsNullOrEmpty(jsonData))
            return default(T);

        try
        {
            var distributedValue = JsonSerializer.Deserialize<T>(jsonData);
            
            // Store in local cache for faster subsequent access
            _localCache.Set(key, distributedValue, _localCacheDuration);
            
            return distributedValue;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize cached value for key: {Key}", key);
            return default(T);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, TimeSpan? absoluteExpiration = null, CancellationToken cancellationToken = default)
    {
        // Set in local cache
        var localOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(_localCacheDuration)
            .SetPriority(CacheItemPriority.Normal);
        
        _localCache.Set(key, value, localOptions);

        // Set in distributed cache
        var distributedOptions = new DistributedCacheEntryOptions();
        
        if (slidingExpiration.HasValue)
            distributedOptions.SetSlidingExpiration(slidingExpiration.Value);
            
        if (absoluteExpiration.HasValue)
            distributedOptions.SetAbsoluteExpiration(absoluteExpiration.Value);

        try
        {
            var jsonData = JsonSerializer.Serialize(value);
            await _distributedCache.SetStringAsync(key, jsonData, distributedOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize value for distributed caching with key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _localCache.Remove(key);
        await _distributedCache.RemoveAsync(key, cancellationToken);
    }

    public Task RemovePatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Pattern-based cache removal not efficiently supported in hybrid cache: {Pattern}", pattern);
        return Task.CompletedTask;
    }
}