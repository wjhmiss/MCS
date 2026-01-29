using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

/// <summary>
/// 提醒Grain实现类，用于创建和管理持久化的提醒任务
/// 支持一次性提醒、循环提醒、有限次数循环、暂停恢复等功能
/// </summary>
public class ReminderGrain : Grain, IReminderGrain, IRemindable
{
    /// <summary>
    /// 持久化状态存储
    /// </summary>
    private readonly IPersistentState<ReminderState> _state;
    
    /// <summary>
    /// 主提醒名称常量
    /// </summary>
    private const string ReminderName = "MainReminder";
    
    /// <summary>
    /// Orleans提醒对象引用
    /// </summary>
    private IGrainReminder? _reminder;

    /// <summary>
    /// 构造函数，注入持久化状态
    /// </summary>
    /// <param name="state">持久化状态对象</param>
    public ReminderGrain(
        [PersistentState("reminder", "Default")] IPersistentState<ReminderState> state)
    {
        _state = state;
    }

    /// <summary>
    /// Grain激活时的初始化逻辑
    /// 恢复之前注册的提醒
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (_state.State.Status == ReminderStatus.Scheduled && _state.State.Period.HasValue)
        {
            var timeUntilReminder = _state.State.ScheduledTime - DateTime.UtcNow;
            
            if (timeUntilReminder <= TimeSpan.Zero && _state.State.NextExecutionAt.HasValue)
            {
                timeUntilReminder = _state.State.NextExecutionAt.Value - DateTime.UtcNow;
            }
            
            if (timeUntilReminder > TimeSpan.Zero)
            {
                await RegisterOrUpdateReminder(timeUntilReminder, _state.State.Period.Value);
            }
        }
    }

    /// <summary>
    /// 创建一个新的提醒
    /// </summary>
    /// <param name="name">提醒名称</param>
    /// <param name="scheduledTime">首次执行时间</param>
    /// <param name="data">自定义数据字典</param>
    /// <param name="period">循环周期（null表示一次性提醒）</param>
    /// <param name="maxExecutions">最大执行次数（null表示无限循环）</param>
    /// <returns>提醒ID</returns>
    public async Task<string> CreateReminderAsync(string name, DateTime scheduledTime, Dictionary<string, object>? data = null, TimeSpan? period = null, int? maxExecutions = null)
    {
        _state.State = new ReminderState
        {
            ReminderId = this.GetPrimaryKeyString(),
            Name = name,
            Status = ReminderStatus.Scheduled,
            ScheduledTime = scheduledTime,
            Period = period,
            MaxExecutions = maxExecutions,
            ExecutionCount = 0,
            CreatedAt = DateTime.UtcNow,
            TriggerHistory = new List<string>(),
            Data = data ?? new Dictionary<string, object>()
        };

        await _state.WriteStateAsync();

        var timeUntilReminder = scheduledTime - DateTime.UtcNow;
        if (timeUntilReminder > TimeSpan.Zero)
        {
            await RegisterOrUpdateReminder(timeUntilReminder, period ?? TimeSpan.FromDays(365));
        }

        return _state.State.ReminderId;
    }

    /// <summary>
    /// 注册或更新Orleans提醒
    /// </summary>
    /// <param name="timeUntilReminder">距离首次执行的时间</param>
    /// <param name="period">循环周期</param>
    private async Task RegisterOrUpdateReminder(TimeSpan timeUntilReminder, TimeSpan period)
    {
        try
        {
            _reminder = await this.RegisterOrUpdateReminder(
                ReminderName,
                timeUntilReminder,
                period);
        }
        catch (Exception ex)
        {
            _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Failed to register reminder: {ex.Message}");
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// 接收Orleans提醒回调
    /// </summary>
    /// <param name="reminderName">提醒名称</param>
    /// <param name="status">提醒状态</param>
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == ReminderName)
        {
            await ExecuteReminderAsync();
        }
    }

    /// <summary>
    /// 执行提醒逻辑
    /// 处理提醒触发、状态更新、循环控制等
    /// </summary>
    private async Task ExecuteReminderAsync()
    {
        try
        {
            _state.State.Status = ReminderStatus.Triggered;
            _state.State.TriggeredAt = DateTime.UtcNow;
            _state.State.ExecutionCount++;

            var triggerLog = $"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' triggered (Execution #{_state.State.ExecutionCount})";
            _state.State.TriggerHistory.Add(triggerLog);

            if (_state.State.MaxExecutions.HasValue && _state.State.ExecutionCount >= _state.State.MaxExecutions.Value)
            {
                _state.State.Status = ReminderStatus.Completed;
                _state.State.CompletedAt = DateTime.UtcNow;
                _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' completed after {_state.State.ExecutionCount} executions");

                if (_reminder != null)
                {
                    await this.UnregisterReminder(_reminder);
                    _reminder = null;
                }
            }
            else if (_state.State.Period.HasValue)
            {
                _state.State.Status = ReminderStatus.Scheduled;
                _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.Period.Value);
                _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' scheduled for next execution at {_state.State.NextExecutionAt}");
            }
            else
            {
                _state.State.Status = ReminderStatus.Completed;
                _state.State.CompletedAt = DateTime.UtcNow;
                _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' completed (one-time reminder)");

                if (_reminder != null)
                {
                    await this.UnregisterReminder(_reminder);
                    _reminder = null;
                }
            }

            await _state.WriteStateAsync();
        }
        catch (Exception ex)
        {
            var errorLog = $"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' error: {ex.Message}";
            _state.State.TriggerHistory.Add(errorLog);
            await _state.WriteStateAsync();
        }
    }

    /// <summary>
    /// 获取提醒的完整状态
    /// </summary>
    /// <returns>提醒状态对象</returns>
    public Task<ReminderState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    /// <summary>
    /// 取消提醒
    /// </summary>
    public async Task CancelAsync()
    {
        if (_state.State.Status != ReminderStatus.Scheduled && _state.State.Status != ReminderStatus.Paused)
        {
            throw new InvalidOperationException("Reminder is not scheduled or paused");
        }

        try
        {
            if (_reminder != null)
            {
                await this.UnregisterReminder(_reminder);
                _reminder = null;
            }
        }
        catch
        {
        }

        _state.State.Status = ReminderStatus.Cancelled;
        _state.State.CancelledAt = DateTime.UtcNow;
        _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' cancelled after {_state.State.ExecutionCount} executions");
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 获取提醒的触发历史记录
    /// </summary>
    /// <returns>触发历史记录列表</returns>
    public Task<List<string>> GetTriggerHistoryAsync()
    {
        return Task.FromResult(_state.State.TriggerHistory);
    }

    /// <summary>
    /// 获取提醒的当前状态
    /// </summary>
    /// <returns>提醒状态枚举</returns>
    public Task<ReminderStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    /// <summary>
    /// 重新调度提醒
    /// </summary>
    /// <param name="newScheduledTime">新的首次执行时间</param>
    public async Task RescheduleAsync(DateTime newScheduledTime)
    {
        if (_state.State.Status == ReminderStatus.Triggered || _state.State.Status == ReminderStatus.Completed)
        {
            throw new InvalidOperationException("Cannot reschedule a triggered or completed reminder");
        }

        try
        {
            if (_reminder != null)
            {
                await this.UnregisterReminder(_reminder);
                _reminder = null;
            }
        }
        catch
        {
        }

        _state.State.Status = ReminderStatus.Scheduled;
        _state.State.ScheduledTime = newScheduledTime;
        _state.State.ExecutionCount = 0;
        _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' rescheduled to {newScheduledTime}");
        await _state.WriteStateAsync();

        var timeUntilReminder = newScheduledTime - DateTime.UtcNow;
        if (timeUntilReminder > TimeSpan.Zero && _state.State.Period.HasValue)
        {
            await RegisterOrUpdateReminder(timeUntilReminder, _state.State.Period.Value);
        }
    }

    /// <summary>
    /// 暂停提醒
    /// </summary>
    public async Task PauseAsync()
    {
        if (_state.State.Status != ReminderStatus.Scheduled)
        {
            throw new InvalidOperationException("Reminder is not scheduled");
        }

        try
        {
            if (_reminder != null)
            {
                await this.UnregisterReminder(_reminder);
                _reminder = null;
            }
        }
        catch
        {
        }

        _state.State.Status = ReminderStatus.Paused;
        _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' paused");
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// 恢复提醒
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_state.State.Status != ReminderStatus.Paused)
        {
            throw new InvalidOperationException("Reminder is not paused");
        }

        if (!_state.State.Period.HasValue)
        {
            throw new InvalidOperationException("Reminder does not have a period configured");
        }

        _state.State.Status = ReminderStatus.Scheduled;
        _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.Period.Value);
        _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' resumed, next execution at {_state.State.NextExecutionAt}");
        await _state.WriteStateAsync();

        await RegisterOrUpdateReminder(_state.State.Period.Value, _state.State.Period.Value);
    }

    /// <summary>
    /// 删除提醒及其所有状态数据
    /// </summary>
    public async Task DeleteAsync()
    {
        try
        {
            if (_reminder != null)
            {
                await this.UnregisterReminder(_reminder);
                _reminder = null;
            }
        }
        catch
        {
        }

        await _state.ClearStateAsync();
    }
}
