using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MyAvaloniaApp.Services
{
    public class RequestQueueManager
    {
        private static RequestQueueManager? _instance;
        public static RequestQueueManager Instance => _instance ??= new RequestQueueManager();

        private readonly ConcurrentQueue<AuthRequest> _authQueue;
        private readonly SemaphoreSlim _processingLimit;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isProcessing;

        private RequestQueueManager()
        {
            _authQueue = new ConcurrentQueue<AuthRequest>();
            _processingLimit = new SemaphoreSlim(5, 5); // Xử lý tối đa 5 request đồng thời
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Bắt đầu background processing
            Task.Run(ProcessQueueAsync);
        }

        public async Task<T> EnqueueRequestAsync<T>(Func<Task<T>> requestFunc, int timeoutMs = 30000)
        {
            var request = new AuthRequest<T>(requestFunc, timeoutMs);
            _authQueue.Enqueue(request);
            
            return await request.TaskCompletionSource.Task;
        }

        private async Task ProcessQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_authQueue.TryDequeue(out var request))
                    {
                        await _processingLimit.WaitAsync(_cancellationTokenSource.Token);
                        
                        // Xử lý request trong background để không block queue
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await request.ExecuteAsync();
                            }
                            finally
                            {
                                _processingLimit.Release();
                            }
                        }, _cancellationTokenSource.Token);
                    }
                    else
                    {
                        // Không có request, chờ một chút
                        await Task.Delay(50, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in queue processing: {ex.Message}");
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
        }

        public void Shutdown()
        {
            _cancellationTokenSource.Cancel();
            _processingLimit.Dispose();
        }
    }

    public abstract class AuthRequest
    {
        public abstract Task ExecuteAsync();
    }

    public class AuthRequest<T> : AuthRequest
    {
        private readonly Func<Task<T>> _requestFunc;
        private readonly int _timeoutMs;
        
        public TaskCompletionSource<T> TaskCompletionSource { get; }

        public AuthRequest(Func<Task<T>> requestFunc, int timeoutMs)
        {
            _requestFunc = requestFunc;
            _timeoutMs = timeoutMs;
            TaskCompletionSource = new TaskCompletionSource<T>();
        }

        public override async Task ExecuteAsync()
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(_timeoutMs);
                var result = await _requestFunc();
                TaskCompletionSource.SetResult(result);
            }
            catch (OperationCanceledException)
            {
                TaskCompletionSource.SetException(new TimeoutException("Request timeout"));
            }
            catch (Exception ex)
            {
                TaskCompletionSource.SetException(ex);
            }
        }
    }
}
