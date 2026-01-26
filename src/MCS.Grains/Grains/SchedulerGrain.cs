using MCS.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Cronos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCS.Grains
{
    public class SchedulerGrain : Grain, ISchedulerGrain, IRemindable
    {
        private readonly ILogger<SchedulerGrain> _logger;
        private readonly IPersistentState<SchedulerState> _persistentState;
        private readonly IGrainFactory _grainFactory;

        public SchedulerGrain(
            ILogger<SchedulerGrain> logger,
            [PersistentState("scheduler", "Default")] IPersistentState<SchedulerState> persistentState,
            IGrainFactory grainFactory)
        {
            _logger = logger;
            _persistentState = persistentState;
            _grainFactory = grainFactory;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            if (_persistentState.RecordExists)
            {
                _state = _persistentState.State;
            }
            StartTimer();
        }

        public async Task<string> ScheduleTaskAsync(string taskDefinitionId, string cronExpression)
        {
            _logger.LogInformation($"Scheduling task {taskDefinitionId} with cron: {cronExpression}");

            var scheduleId = Guid.NewGuid().ToString();
            var cron = CronExpression.Parse(cronExpression);
            var nextRunTime = cron.GetNextOccurrence(DateTime.UtcNow);

            var schedule = new ScheduleInfo
            {
                Id = scheduleId,
                TaskDefinitionId = taskDefinitionId,
                CronExpression = cronExpression,
                IsEnabled = true,
                NextRunTime = nextRunTime?.ToString("O") ?? string.Empty
            };

            _state.Schedules[scheduleId] = schedule;
            await _persistentState.WriteStateAsync();

            await this.RegisterOrUpdateReminder(scheduleId, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            return $"Task scheduled with ID: {scheduleId}";
        }

        public async Task<string> ScheduleWorkflowAsync(string workflowDefinitionId, string cronExpression)
        {
            _logger.LogInformation($"Scheduling workflow {workflowDefinitionId} with cron: {cronExpression}");

            var scheduleId = Guid.NewGuid().ToString();
            var cron = CronExpression.Parse(cronExpression);
            var nextRunTime = cron.GetNextOccurrence(DateTime.UtcNow);

            var schedule = new ScheduleInfo
            {
                Id = scheduleId,
                WorkflowDefinitionId = workflowDefinitionId,
                CronExpression = cronExpression,
                IsEnabled = true,
                NextRunTime = nextRunTime?.ToString("O") ?? string.Empty
            };
            _state.Schedules[scheduleId] = schedule;
            await _persistentState.WriteStateAsync();

            await this.RegisterOrUpdateReminder(scheduleId, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            return $"Workflow scheduled with ID: {scheduleId}";
        }

        public async Task<string> UnscheduleAsync(string scheduleId)
        {
            _logger.LogInformation($"Unscheduling {scheduleId}");

            if (_state.Schedules.ContainsKey(scheduleId))
            {
                _state.Schedules.Remove(scheduleId);
                await _persistentState.WriteStateAsync();

                try
                {
                    var reminder = await this.GetReminder(scheduleId);
                    if (reminder != null)
                    {
                        await this.UnregisterReminder(reminder);
                    }
                }
                catch
                {
                }

                return $"Schedule {scheduleId} removed";
            }

            return $"Schedule {scheduleId} not found";
        }

        public async Task<string> UpdateScheduleAsync(string scheduleId, string cronExpression)
        {
            _logger.LogInformation($"Updating schedule {scheduleId} with new cron: {cronExpression}");

            if (_state.Schedules.ContainsKey(scheduleId))
            {
                var schedule = _state.Schedules[scheduleId];
                schedule = schedule with { CronExpression = cronExpression };
                _state.Schedules[scheduleId] = schedule;
                await _persistentState.WriteStateAsync();

                return $"Schedule {scheduleId} updated";
            }

            return $"Schedule {scheduleId} not found";
        }

        public Task<List<ScheduleInfo>> GetSchedulesAsync()
        {
            return Task.FromResult(_state.Schedules.Values.ToList());
        }

        public async Task ReceiveReminder(string reminderName, TickStatus status)
        {
            _logger.LogInformation($"Reminder triggered: {reminderName}");

            if (_state.Schedules.TryGetValue(reminderName, out var schedule) && schedule.IsEnabled)
            {
                var cron = CronExpression.Parse(schedule.CronExpression);
                var now = DateTime.UtcNow;
                var nextRunTime = cron.GetNextOccurrence(now);

                if (nextRunTime.HasValue && nextRunTime.Value <= now.AddMinutes(1))
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(schedule.TaskDefinitionId))
                        {
                            var taskGrain = _grainFactory.GetGrain<ITaskGrain>(schedule.TaskDefinitionId);
                            await taskGrain.ExecuteAsync(new Dictionary<string, object>());
                        }
                        else if (!string.IsNullOrEmpty(schedule.WorkflowDefinitionId))
                        {
                            var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(schedule.WorkflowDefinitionId);
                            await workflowGrain.StartAsync(new Dictionary<string, object>());
                        }

                        var updatedSchedule = schedule with
                        {
                            LastRunTime = now.ToString("O"),
                            NextRunTime = cron.GetNextOccurrence(now.AddMinutes(1))?.ToString("O") ?? string.Empty
                        };
                        _state.Schedules[reminderName] = updatedSchedule;
                        await _persistentState.WriteStateAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to execute scheduled task/workflow: {reminderName}");
                    }
                }
            }
        }

        private IDisposable? _timer;

        private void StartTimer()
        {
            _timer = this.RegisterGrainTimer(
                async _ =>
                {
                    await CheckSchedulesAsync();
                },
                new()
                {
                    DueTime = TimeSpan.Zero,
                    Period = TimeSpan.FromMinutes(1),
                    Interleave = true
                });
        }

        private async Task CheckSchedulesAsync()
        {
            var now = DateTime.UtcNow;
            foreach (var (scheduleId, schedule) in _state.Schedules.ToList())
            {
                if (!schedule.IsEnabled) continue;

                try
                {
                    var cron = CronExpression.Parse(schedule.CronExpression);
                    var nextRunTime = cron.GetNextOccurrence(now);

                    if (nextRunTime.HasValue && nextRunTime.Value <= now.AddMinutes(1))
                    {
                        if (!string.IsNullOrEmpty(schedule.TaskDefinitionId))
                        {
                            var taskGrain = _grainFactory.GetGrain<ITaskGrain>(schedule.TaskDefinitionId);
                            await taskGrain.ExecuteAsync(new Dictionary<string, object>());
                        }
                        else if (!string.IsNullOrEmpty(schedule.WorkflowDefinitionId))
                        {
                            var workflowGrain = _grainFactory.GetGrain<IWorkflowGrain>(schedule.WorkflowDefinitionId);
                            await workflowGrain.StartAsync(new Dictionary<string, object>());
                        }

                        var updatedSchedule = schedule with
                        {
                            LastRunTime = now.ToString("O"),
                            NextRunTime = cron.GetNextOccurrence(now.AddMinutes(1))?.ToString("O") ?? string.Empty
                        };
                        _state.Schedules[scheduleId] = updatedSchedule;
                        await _persistentState.WriteStateAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to execute scheduled task/workflow: {scheduleId}");
                }
            }
        }

        public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            return base.OnDeactivateAsync(reason, cancellationToken);
        }

        private SchedulerState _state = new();
    }

    public class SchedulerState
    {
        public Dictionary<string, ScheduleInfo> Schedules { get; set; } = new();
    }
}
