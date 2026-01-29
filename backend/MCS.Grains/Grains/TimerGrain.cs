using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class TimerGrain : Grain, ITimerGrain
{
    private readonly IPersistentState<TimerState> _state;
    private IGrainTimer? _timer;

    public TimerGrain(
        [PersistentState("timer", "Default")] IPersistentState<TimerState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (_state.State.Status == TimerStatus.Active)
        {
            StartTimer();
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task<string> CreateTimerAsync(string name, TimeSpan interval, Dictionary<string, object>? data = null)
    {
        _state.State = new TimerState
        {
            TimerId = this.GetPrimaryKeyString(),
            Name = name,
            Status = TimerStatus.Stopped,
            Interval = interval,
            CreatedAt = DateTime.UtcNow,
            ExecutionCount = 0,
            ExecutionLogs = new List<string>(),
            Data = data ?? new Dictionary<string, object>()
        };

        await _state.WriteStateAsync();
        return _state.State.TimerId;
    }

    public Task<TimerState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

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

    private async Task ExecuteTimerAsync()
    {
        try
        {
            _state.State.ExecutionCount++;
            _state.State.LastExecutedAt = DateTime.UtcNow;
            _state.State.NextExecutionAt = DateTime.UtcNow.Add(_state.State.Interval);

            var log = $"[{DateTime.UtcNow}] Timer '{_state.State.Name}' executed (Execution #{_state.State.ExecutionCount})";
            _state.State.ExecutionLogs.Add(log);

            await _state.WriteStateAsync();
        }
        catch (Exception ex)
        {
            var errorLog = $"[{DateTime.UtcNow}] Timer '{_state.State.Name}' error: {ex.Message}";
            _state.State.ExecutionLogs.Add(errorLog);
            await _state.WriteStateAsync();
        }
    }

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

    public async Task StopAsync()
    {
        _timer?.Dispose();
        _timer = null;

        _state.State.Status = TimerStatus.Stopped;
        _state.State.ExecutionLogs.Add($"[{DateTime.UtcNow}] Timer '{_state.State.Name}' stopped");
        await _state.WriteStateAsync();
    }

    public Task<List<string>> GetExecutionLogsAsync()
    {
        return Task.FromResult(_state.State.ExecutionLogs);
    }

    public Task<TimerStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    public async Task UpdateIntervalAsync(TimeSpan newInterval)
    {
        _state.State.Interval = newInterval;

        if (_state.State.Status == TimerStatus.Active)
        {
            StartTimer();
        }

        await _state.WriteStateAsync();
    }

    public async Task DeleteAsync()
    {
        _timer?.Dispose();
        _timer = null;
        await _state.ClearStateAsync();
    }
}