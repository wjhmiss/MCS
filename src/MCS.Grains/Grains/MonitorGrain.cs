using MCS.Grains.Interfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MCS.Grains
{
    public class MonitorGrain : Grain, IMonitorGrain
    {
        private readonly ILogger<MonitorGrain> _logger;
        private readonly IPersistentState<MonitorState> _persistentState;

        public MonitorGrain(
            ILogger<MonitorGrain> logger,
            [PersistentState("monitor", "Default")] IPersistentState<MonitorState> persistentState)
        {
            _logger = logger;
            _persistentState = persistentState;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            if (_persistentState.RecordExists)
            {
                _state = _persistentState.State;
            }
        }

        public async Task<string> LogAlertAsync(AlertInfo alert)
        {
            _logger.LogInformation($"Logging alert: {alert.Title}");

            var alertWithId = alert with { Id = Guid.NewGuid().ToString() };
            _state.Alerts[alertWithId.Id] = alertWithId;

            if (_state.Alerts.Count > 1000)
            {
                var oldestAlert = _state.Alerts.OrderBy(a => a.Value.CreatedAt).First();
                _state.Alerts.Remove(oldestAlert.Key);
            }

            await _persistentState.WriteStateAsync();
            return $"Alert logged: {alertWithId.Id}";
        }

        public Task<List<AlertInfo>> GetAlertsAsync(int limit = 100)
        {
            var alerts = _state.Alerts.Values
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
                .ToList();

            return Task.FromResult(alerts);
        }

        public async Task<string> ResolveAlertAsync(string alertId)
        {
            _logger.LogInformation($"Resolving alert: {alertId}");

            if (_state.Alerts.TryGetValue(alertId, out var alert))
            {
                var resolvedAlert = alert with
                {
                    IsResolved = true,
                    ResolvedAt = DateTime.UtcNow.ToString("O")
                };
                _state.Alerts[alertId] = resolvedAlert;
                await _persistentState.WriteStateAsync();
                return $"Alert {alertId} resolved";
            }

            return $"Alert {alertId} not found";
        }

        public Task<Dictionary<string, object>> GetSystemHealthAsync()
        {
            var health = new Dictionary<string, object>
            {
                { "totalAlerts", _state.Alerts.Count },
                { "unresolvedAlerts", _state.Alerts.Values.Count(a => !a.IsResolved) },
                { "criticalAlerts", _state.Alerts.Values.Count(a => a.Severity == "Critical" && !a.IsResolved) },
                { "lastAlertTime", _state.Alerts.Values.Any() ? _state.Alerts.Values.Max(a => a.CreatedAt) : "None" }
            };

            return Task.FromResult(health);
        }

        private MonitorState _state = new();
    }

    public class MonitorState
    {
        public Dictionary<string, AlertInfo> Alerts { get; set; } = new();
    }
}
