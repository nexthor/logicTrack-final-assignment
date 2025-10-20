using Cap1.LogiTrack.Services;
using System.Diagnostics;

namespace Cap1.LogiTrack.Examples;

public static class PerformanceComparisonExample
{
    public static async Task ComparePerformanceAsync()
    {
        using var context = new LogiTrackContext();
        var service = new OrderSummaryService(context);
        
        Console.WriteLine("⚡ Performance Comparison of Order Summary Methods\n");
        
        var methods = new Dictionary<string, Func<Task>>
        {
            ["Simple Projection (SQLite)"] = service.PrintSimpleOrderSummariesAsync,
            ["Advanced Projection"] = service.PrintOrderSummariesWithProjectionAsync,
            ["Include Method"] = service.PrintOrderSummariesWithIncludeAsync,
            ["Table Format"] = service.PrintOrderSummariesAsTableAsync,
            ["Quick Stats"] = service.PrintQuickOrderStatsAsync
        };
        
        var results = new List<(string Method, long ElapsedMs)>();
        
        foreach (var method in methods)
        {
            Console.WriteLine($"🔄 Testing: {method.Key}");
            
            var stopwatch = Stopwatch.StartNew();
            
            // Capture console output to avoid cluttering performance results
            var originalOut = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                await method.Value();
            }
            catch (Exception ex)
            {
                Console.SetOut(originalOut);
                Console.WriteLine($"❌ Error in {method.Key}: {ex.Message}");
                continue;
            }
            
            stopwatch.Stop();
            Console.SetOut(originalOut);
            
            results.Add((method.Key, stopwatch.ElapsedMilliseconds));
            Console.WriteLine($"✅ Completed in {stopwatch.ElapsedMilliseconds}ms\n");
        }
        
        // Display performance summary
        Console.WriteLine("📊 Performance Summary:");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"{"Method",-25} {"Time (ms)",-10}");
        Console.WriteLine(new string('-', 50));
        
        foreach (var (method, time) in results.OrderBy(r => r.ElapsedMs))
        {
            var emoji = time switch
            {
                < 10 => "🚀",
                < 50 => "⚡",
                < 100 => "✅",
                _ => "⏳"
            };
            Console.WriteLine($"{method,-25} {time,-10} {emoji}");
        }
        
        Console.WriteLine(new string('=', 50));
        
        if (results.Any())
        {
            var fastest = results.OrderBy(r => r.ElapsedMs).First();
            var slowest = results.OrderBy(r => r.ElapsedMs).Last();
            
            Console.WriteLine($"🏆 Fastest: {fastest.Method} ({fastest.ElapsedMs}ms)");
            Console.WriteLine($"🐌 Slowest: {slowest.Method} ({slowest.ElapsedMs}ms)");
            
            if (slowest.ElapsedMs > 0)
            {
                var speedup = (double)slowest.ElapsedMs / fastest.ElapsedMs;
                Console.WriteLine($"📈 Speed difference: {speedup:F1}x faster");
            }
        }
    }
}