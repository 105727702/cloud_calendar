using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MyAvaloniaApp.Services
{
    public class PerformanceMonitor
    {
        private static PerformanceMonitor? _instance;
        public static PerformanceMonitor Instance => _instance ??= new PerformanceMonitor();

        private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
        private readonly Timer _reportTimer;

        private PerformanceMonitor()
        {
            _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
            
            // Report metrics every 60 seconds
            _reportTimer = new Timer(ReportMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public PerformanceMeasurement StartMeasurement(string operationName)
        {
            return new PerformanceMeasurement(this, operationName);
        }

        public void RecordMetric(string operationName, TimeSpan duration, bool success = true)
        {
            var metric = _metrics.GetOrAdd(operationName, _ => new PerformanceMetric());
            
            lock (metric)
            {
                metric.TotalCalls++;
                metric.TotalDuration += duration;
                
                if (success)
                {
                    metric.SuccessfulCalls++;
                }
                
                if (metric.MinDuration == TimeSpan.Zero || duration < metric.MinDuration)
                {
                    metric.MinDuration = duration;
                }
                
                if (duration > metric.MaxDuration)
                {
                    metric.MaxDuration = duration;
                }
                
                metric.LastCallTime = DateTime.UtcNow;
            }
        }

        public PerformanceMetric? GetMetric(string operationName)
        {
            _metrics.TryGetValue(operationName, out var metric);
            return metric;
        }

        private void ReportMetrics(object? state)
        {
            Console.WriteLine("=== Performance Report ===");
            
            foreach (var kvp in _metrics)
            {
                var metric = kvp.Value;
                lock (metric)
                {
                    if (metric.TotalCalls > 0)
                    {
                        var avgDuration = TimeSpan.FromTicks(metric.TotalDuration.Ticks / metric.TotalCalls);
                        var successRate = (double)metric.SuccessfulCalls / metric.TotalCalls * 100;
                        
                        Console.WriteLine($"{kvp.Key}:");
                        Console.WriteLine($"  Total Calls: {metric.TotalCalls}");
                        Console.WriteLine($"  Success Rate: {successRate:F1}%");
                        Console.WriteLine($"  Avg Duration: {avgDuration.TotalMilliseconds:F1}ms");
                        Console.WriteLine($"  Min Duration: {metric.MinDuration.TotalMilliseconds:F1}ms");
                        Console.WriteLine($"  Max Duration: {metric.MaxDuration.TotalMilliseconds:F1}ms");
                        Console.WriteLine();
                    }
                }
            }
        }

        public void ClearMetrics()
        {
            _metrics.Clear();
        }

        public void Dispose()
        {
            _reportTimer?.Dispose();
            _metrics.Clear();
        }
    }

    public class PerformanceMetric
    {
        public int TotalCalls { get; set; }
        public int SuccessfulCalls { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan MinDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public DateTime LastCallTime { get; set; }
    }

    public class PerformanceMeasurement : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _success = true;

        public PerformanceMeasurement(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void MarkFailure()
        {
            _success = false;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor.RecordMetric(_operationName, _stopwatch.Elapsed, _success);
        }
    }
}
