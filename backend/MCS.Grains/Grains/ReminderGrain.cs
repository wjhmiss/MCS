using Orleans;
using Orleans.Runtime;
using MCS.Grains.Interfaces;
using MCS.Grains.Models;

namespace MCS.Grains.Grains;

public class ReminderGrain : Grain, IReminderGrain, IRemindable
{
    private readonly IPersistentState<ReminderState> _state;
    private const string ReminderName = "MainReminder";
    private IGrainReminder? _reminder;

    public ReminderGrain(
        [PersistentState("reminder", "Default")] IPersistentState<ReminderState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (_state.State.Status == ReminderStatus.Scheduled)
        {
            var timeUntilReminder = _state.State.ScheduledTime - DateTime.UtcNow;
            if (timeUntilReminder > TimeSpan.Zero)
            {
                await RegisterOrUpdateReminder(timeUntilReminder);
            }
        }
    }

    public async Task<string> CreateReminderAsync(string name, DateTime scheduledTime, Dictionary<string, object>? data = null)
    {
        _state.State = new ReminderState
        {
            ReminderId = this.GetPrimaryKeyString(),
            Name = name,
            Status = ReminderStatus.Scheduled,
            ScheduledTime = scheduledTime,
            CreatedAt = DateTime.UtcNow,
            TriggerHistory = new List<string>(),
            Data = data ?? new Dictionary<string, object>()
        };

        await _state.WriteStateAsync();

        var timeUntilReminder = scheduledTime - DateTime.UtcNow;
        if (timeUntilReminder > TimeSpan.Zero)
        {
            await RegisterOrUpdateReminder(timeUntilReminder);
        }

        return _state.State.ReminderId;
    }

    private async Task RegisterOrUpdateReminder(TimeSpan timeUntilReminder)
    {
        try
        {
            _reminder = await this.RegisterOrUpdateReminder(
                ReminderName,
                timeUntilReminder,
                TimeSpan.FromDays(365));
        }
        catch (Exception ex)
        {
            _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Failed to register reminder: {ex.Message}");
            await _state.WriteStateAsync();
        }
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == ReminderName)
        {
            await ExecuteReminderAsync();
        }
    }

    private async Task ExecuteReminderAsync()
    {
        try
        {
            _state.State.Status = ReminderStatus.Triggered;
            _state.State.TriggeredAt = DateTime.UtcNow;

            var triggerLog = $"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' triggered";
            _state.State.TriggerHistory.Add(triggerLog);

            await _state.WriteStateAsync();

            if (_reminder != null)
            {
                await this.UnregisterReminder(_reminder);
                _reminder = null;
            }
        }
        catch (Exception ex)
        {
            var errorLog = $"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' error: {ex.Message}";
            _state.State.TriggerHistory.Add(errorLog);
            await _state.WriteStateAsync();
        }
    }

    public Task<ReminderState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task CancelAsync()
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

        _state.State.Status = ReminderStatus.Cancelled;
        _state.State.CancelledAt = DateTime.UtcNow;
        _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' cancelled");
        await _state.WriteStateAsync();
    }

    public Task<List<string>> GetTriggerHistoryAsync()
    {
        return Task.FromResult(_state.State.TriggerHistory);
    }

    public Task<ReminderStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    public async Task RescheduleAsync(DateTime newScheduledTime)
    {
        if (_state.State.Status == ReminderStatus.Triggered)
        {
            throw new InvalidOperationException("Cannot reschedule a triggered reminder");
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
        _state.State.TriggerHistory.Add($"[{DateTime.UtcNow}] Reminder '{_state.State.Name}' rescheduled to {newScheduledTime}");
        await _state.WriteStateAsync();

        var timeUntilReminder = newScheduledTime - DateTime.UtcNow;
        if (timeUntilReminder > TimeSpan.Zero)
        {
            await RegisterOrUpdateReminder(timeUntilReminder);
        }
    }

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