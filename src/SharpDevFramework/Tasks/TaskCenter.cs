using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpDevLib;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;

namespace SharpDevFramework;

/// <summary>
/// 任务中心，负责管理后台任务的发布、消费、重试和状态更新
/// </summary>
public class TaskCenter(ILogger<TaskCenter> logger, IServiceProvider serviceProvider, IConfiguration configuration) : ISingletonService
{
    Channel<int>? _channel;
    bool _started;
    Task? _consumerTask;
    CancellationTokenSource? _stoppingTokenSource;
    readonly Dictionary<int, CancellationTokenSource> _cancleTokens = [];
    readonly Dictionary<string, Type> _handlerTypes = [];
    readonly HashSet<string> _sequentialTypes = [];
    readonly ConcurrentDictionary<string, SemaphoreSlim> _sequentialSemaphores = [];

    /// <summary>
    /// 发布任务到队列
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    public async Task PublishAsync(int taskId)
    {
        if (!_started || _channel is null) throw new Exception($"{nameof(TaskCenter)} not started");
        await _channel.Writer.WriteAsync(taskId);
    }

    /// <summary>
    /// 启动任务中心
    /// </summary>
    /// <param name="assemblies">需要扫描的程序集</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Task</returns>
    internal Task StartAsync(Assembly[] assemblies, CancellationToken cancellationToken)
    {
        if (_started) throw new InvalidOperationException($"{nameof(TaskCenter)} already started");
        _started = true;
        RegisterHandlerTypes(assemblies);
        cancellationToken.Register(async () => await StopAsync());
        _channel = Channel.CreateUnbounded<int>();
        _stoppingTokenSource = new CancellationTokenSource();
        _consumerTask = RunConsumerAsync(_stoppingTokenSource.Token);
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("{TaskCenter} started", nameof(TaskCenter));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 注册任务处理器类型
    /// </summary>
    /// <param name="assemblies">需要扫描的程序集</param>
    void RegisterHandlerTypes(Assembly[] assemblies)
    {
        assemblies
           .SelectMany(a => a.GetTypes())
           .Distinct()
           .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseTask).IsAssignableFrom(t))
           .ForEach(x =>
           {
               var attribute = x.GetCustomAttribute<TaskRegisterAttribute>();
               if (attribute is null)
               {
                   if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("type '{Type}' missing TaskRegisterAttribute", x.FullName);
                   return;
               }
               if (_handlerTypes.TryGetValue(attribute.Type, out _)) throw new Exception($"Task type '{attribute.Type}' is already registered");
               _handlerTypes[attribute.Type] = x;

               var sequentialAttr = x.GetCustomAttribute<TaskSequentialAttribute>();
               if (sequentialAttr is not null)
               {
                   _sequentialTypes.Add(attribute.Type);
               }
           });
    }

    /// <summary>
    /// 运行任务消费者
    /// </summary>
    /// <param name="stoppingToken">停止令牌</param>
    async Task RunConsumerAsync(CancellationToken stoppingToken)
    {
        try
        {
            var maxProcessCount = configuration.GetValue<int>("FrameworkSettings:Tasks:MaxProcessCount");
            var semaphore = new SemaphoreSlim(maxProcessCount, maxProcessCount);
            await foreach (var taskId in _channel!.Reader.ReadAllAsync(stoppingToken))
            {
                await semaphore.WaitAsync(stoppingToken);

                // 判断任务类型是否标记了串行执行
                bool needSequentialControl = false;
                string? taskType = null;
                try
                {
                    var dbContext = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<FrameworkDbContext>();
                    var task = dbContext.Tasks.Find(taskId);
                    if (task is not null)
                    {
                        taskType = task.Type;
                        needSequentialControl = _sequentialTypes.Contains(task.Type);
                    }
                }
                catch { /* ignore */ }

                SemaphoreSlim? sequentialSemaphore = null;
                if (needSequentialControl && taskType is not null)
                {
                    sequentialSemaphore = _sequentialSemaphores.GetOrAdd(taskType, _ => new SemaphoreSlim(1, 1));
                    await sequentialSemaphore.WaitAsync(stoppingToken);
                }

                _ = ProcessSingleTask(taskId, semaphore, sequentialSemaphore).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        logger.LogError(t.Exception, "Task processing failed with unobserved exception");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Task consumer stopped");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Task consumer fatal error");
        }
    }

    /// <summary>
    /// 取消指定任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <returns>Task</returns>
    public async Task CancelTaskAsync(int taskId)
    {
        await ChangeTaskState(taskId, TaskStates.Failed, "cancled");
        if (_cancleTokens.TryGetValue(taskId, out var cts))
        {
            await cts.CancelAsync();
        }
    }

    /// <summary>
    /// 停止任务中心
    /// </summary>
    /// <returns>Task</returns>
    internal async Task StopAsync()
    {
        if (!_started) return;
        _stoppingTokenSource?.Cancel();
        _channel?.Writer.Complete();
        if (_consumerTask != null) await _consumerTask.WaitAsync(TimeSpan.FromSeconds(5));
        _channel = null;
        _started = false;
        foreach (var item in _cancleTokens)
        {
            await item.Value.CancelAsync();
        }
        _stoppingTokenSource?.Dispose();
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("{TaskCenter} stopped", nameof(TaskCenter));
    }

    /// <summary>
    /// 处理单个任务
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="semaphore">全局信号量</param>
    /// <param name="sequentialSemaphore">串行控制信号量（同一任务类型不允许并发时使用）</param>
    async Task ProcessSingleTask(int taskId, SemaphoreSlim semaphore, SemaphoreSlim? sequentialSemaphore)
    {
        try
        {
            var dbContext = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<FrameworkDbContext>();
            var task = dbContext.Tasks.Find(taskId);
            if (task is null) return;

            if (!_handlerTypes.TryGetValue(task.Type, out var handlerType))
            {
                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("{Message} ignored", task.Type);
                return;
            }
            var handler = serviceProvider.CreateScope().ServiceProvider.GetRequiredService(handlerType) as BaseTask ?? throw new Exception($"Task type '{handlerType.FullName}' must inherit from {nameof(BaseTask)}");
            _cancleTokens[taskId] = new CancellationTokenSource();
            await handler.HandleAsync(taskId, _cancleTokens[taskId].Token);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error)) logger.LogError("Task error: {Message}", ex.Message);
        }
        finally
        {
            _cancleTokens.Remove(taskId);
            sequentialSemaphore?.Release();
            semaphore.Release();
        }
    }

    /// <summary>
    /// 更改任务状态
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="state">新状态</param>
    /// <param name="error">错误信息（可选）</param>
    /// <returns>Task</returns>
    public async Task ChangeTaskState(int taskId, TaskStates state, string? error = null)
    {
        var dbContext = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<FrameworkDbContext>();
        var task = dbContext.Tasks.Find(taskId);
        if (task is null) return;
        if (task.Status == TaskStates.Completed) return;

        task.Status = state;
        task.UpdatedAt = DateTime.Now.ToUtcTimestamp();
        if (error.NotNullOrWhiteSpace()) task.ErrorMessage = error;
        if (state == TaskStates.Completed) task.ErrorMessage = null;
        dbContext.Tasks.Update(task);
        dbContext.SaveChanges();

        await serviceProvider.GetRequiredService<NotificationService>().BroadcastTaskUpdatedAsync(task);
        if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("{TaskType} task {TaskId} {TaskState}", task.Type, task.Id, task.Status);

        if (task.Status == TaskStates.Failed)
        {
            var maxRetryCount = configuration.GetValue<int>("FrameworkSettings:Tasks:MaxRetryCount");
            if (task.RetryCount < maxRetryCount)
            {
                task.RetryCount += 1;
                dbContext.Tasks.Update(task);
                dbContext.SaveChanges();
                await PublishAsync(task.Id);
                if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("Process {TaskType} task {TaskId} Failed, retrying... (RetryCount: {RetryCount})", task.Type, task.Id, task.RetryCount);
            }
            else
            {
                if (logger.IsEnabled(LogLevel.Error)) logger.LogError("Process {TaskType} task {TaskId} Failed after maximum retries", task.Type, task.Id);
            }
        }
    }
}