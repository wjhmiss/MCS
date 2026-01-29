using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

/// <summary>
/// 定时器Grain实现类，用于创建和管理内存中的定时任务
/// 支持高精度定时、暂停恢复、动态调整间隔等功能
/// </summary>
public class TimerGrain : Grain, ITimerGrain
{
    /// <summary>
    /// 持久化状态存储
    /// </summary>
    private readonly IPersistentState<TimerState> _state;
    
    /// <summary>
    /// Orleans定时器对象引用
    /// </summary>
    private IGrainTimer? _timer;

    /// <summary>
    /// 构造函数，注入持久化状态
    /// </summary>
    /// <param name="state">持久化状态对象</param>
    public TimerGrain(
        [PersistentState("timer", "Default")] IPersistentState<TimerState> state)
    {
        _state = state;
    }

    /// <summary>
    /// Grain激活时的初始化逻辑
    /// 如果定时器处于活动状态，则重新启动定时器
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (_state.State.Status == TimerStatus.Active)
        {
            StartTimer();
        }
    }

    /// <summary>
    /// Grain停用时的清理逻辑
    /// 释放定时器资源
    /// </summary>
    /// <param name="reason">停用原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    /// <summary>
    /// 创建一个新的定时器
    /// </summary>
    /// <param name="name">定时器名称</param>
    /// <param name="interval">执行间隔时间</param>
    /// <param name="data">自定义数据字典</param>
    /// <param name="maxExecutions">最大执行次数（null表示无限执行）</param>
    /// <returns>定时器ID</returns>
    public async Task<string> CreateTimerAsync(string name, TimeSpan interval, Dictionary<string, object>? data = null, int? maxExecutions = null)
    {
        _state.State = new TimerState
        {
            TimerId = this.GetPrimaryKeyString(),
            Name = name,
            Status = TimerStatus.Stopped,
            Interval = interval,
            CreatedAt = DateTime.UtcNow,
            ExecutionCount = 0,
            MaxExecutions = maxExecutions,
            ExecutionLogs = new List<string>(),
            Data = data ?? new Dictionary<string, object>()
        };

        await _state.WriteStateAsync();
        return _state.State.TimerId;
    }

    /// <summary>
    /// 获取定时器的完整状态
    /// </summary>
    /// <returns>定时器状态对象</returns>
    public Task<TimerState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    /// <summary>
    /// 启动定时器
    /// </summary>
    public async Task StartAsync()
    {
        if (_state.State.Status == TimerStatus.Active)
        {
            throw new InvalidOperationException("Timer is already active");
        }

        _state.State.Status = TimerStatus.Active;
        _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.Interval);
        await _state.WriteStateAsync();

        StartTimer();
    }

    /// <summary>
    /// 启动Orleans定时器
    /// </summary>
    private void StartTimer()
    {
        _timer?.Dispose();
        _timer = this.RegisterGrainTimer(
            async _ =>
            {
                await ExecuteTimerAsync();
            },
            new GrainTimerCreationOptions
            {
                DueTime = _state.State.Interval,
                Period = _state.State.Interval,
                Interleave = true
            });
    }

    /// <summary>
    /// 执行定时器逻辑
    /// 处理定时器触发、状态更新、执行次数控制等
    /// </summary>
    private async Task ExecuteTimerAsync()
    {
        try
        {
            _state.State.ExecutionCount++;
            _state.State.LastExecutedAt = DateTime.UtcNow;

            var log = $"[{DateTime.UtcNow}] Timer '{_state.State.Name}' executed (Execution #{_state.State.ExecutionCount})";
            _state.State.ExecutionLogs.Add(log);

            if (_state.State.MaxExecutions.HasValue && _state.State.ExecutionCount >= _state.State.MaxExecutions.Value)
            {
                _state.State.Status = TimerStatus.Stopped;
                _state.State.ExecutionLogs.Add($"[{DateTime.UtcNow}] Timer '{_state.State.Name}' reached max executions ({_state.State.MaxExecutions.Value}) and stopped");
                _timer?.Dispose();
                _timer = null;
            }
            else
            {
                _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.Interval);
            }

            await _state.WriteStateAsync();
        }
        catch (Exception ex)
        {
            var errorLog = $"[{DateTime.UtcNow}] Timer '{_state.State.Name}' error: {ex.Message}";
            _state.State.ExecutionLogs.Add(errorLog);
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// 暂停定时器
    /// </summary>
    public async Task PauseAsync()
    {
        if (_state.State.Status != TimerStatus.Active)
        {
            throw new InvalidOperationException("Timer is not active");
        }

        _timer?.Dispose();
        _timer = null;

        _state.State.Status = TimerStatus.Paused;
        _state.State.ExecutionLogs.Add($"[{DateTime.UtcNow}] Timer '{_state.State.Name}' paused");
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 恢复定时器
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_state.State.Status != TimerStatus.Paused)
        {
            throw new InvalidOperationException("Timer is not paused");
        }

        _state.State.Status = TimerStatus.Active;
        _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.Interval);
        _state.State.ExecutionLogs.Add($"[{DateTime.UtcNow}] Timer '{_state.State.Name}' resumed");
        await _state.WriteStateAsync();

        StartTimer();
    }

    /// <summary>
    /// 停止定时器
    /// </summary>
    public async Task StopAsync()
    {
        _timer?.Dispose();
        _timer = null;

        _state.State.Status = TimerStatus.Stopped;
        _state.State.ExecutionLogs.Add($"[{DateTime.UtcNow}] Timer '{_state.State.Name}' stopped");
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 获取定时器的执行日志
    /// </summary>
    /// <returns>执行日志列表</returns>
    public Task<List<string>> GetExecutionLogsAsync()
    {
        return Task.FromResult(_state.State.ExecutionLogs);
    }

    /// <summary>
    /// 获取定时器的当前状态
    /// </summary>
    /// <returns>定时器状态枚举</returns>
    public Task<TimerStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    /// <summary>
    /// 更新定时器的执行间隔
    /// </summary>
    /// <param name="newInterval">新的执行间隔</param>
    public async Task UpdateIntervalAsync(TimeSpan newInterval)
    {
        _state.State.Interval = newInterval;

        if (_state.State.Status == TimerStatus.Active)
        {
            StartTimer();
        }

        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 删除定时器及其所有状态数据
    /// </summary>
    public async Task DeleteAsync()
    {
        _timer?.Dispose();
        _timer = null;
        await _state.ClearStateAsync();
    }
}
