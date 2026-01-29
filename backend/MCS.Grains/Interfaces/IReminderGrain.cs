using Orleans;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

/// <summary>
/// 提醒Grain接口，用于创建和管理持久化的提醒任务
/// 支持一次性提醒、循环提醒、有限次数循环等功能
/// </summary>
public interface IReminderGrain : IGrainWithStringKey
{
    /// <summary>
    /// 创建一个新的提醒
    /// </summary>
    /// <param name="name">提醒名称</param>
    /// <param name="scheduledTime">首次执行时间</param>
    /// <param name="data">自定义数据字典</param>
    /// <param name="period">循环周期（null表示一次性提醒）</param>
    /// <param name="maxExecutions">最大执行次数（null表示无限循环）</param>
    /// <returns>提醒ID</returns>
    Task<string> CreateReminderAsync(string name, DateTime scheduledTime, Dictionary<string, object>? data = null, TimeSpan? period = null, int? maxExecutions = null);
    
    /// <summary>
    /// 获取提醒的完整状态
    /// </summary>
    /// <returns>提醒状态对象</returns>
    Task<ReminderState> GetStateAsync();
    
    /// <summary>
    /// 取消提醒（仅当提醒处于Scheduled或Paused状态时可用）
    /// </summary>
    Task CancelAsync();
    
    /// <summary>
    /// 获取提醒的触发历史记录
    /// </summary>
    /// <returns>触发历史记录列表</returns>
    Task<List<string>> GetTriggerHistoryAsync();
    
    /// <summary>
    /// 获取提醒的当前状态
    /// </summary>
    /// <returns>提醒状态枚举</returns>
    Task<ReminderStatus> GetStatusAsync();
    
    /// <summary>
    /// 重新调度提醒（仅当提醒未触发或未完成时可用）
    /// </summary>
    /// <param name="newScheduledTime">新的首次执行时间</param>
    Task RescheduleAsync(DateTime newScheduledTime);
    
    /// <summary>
    /// 暂停提醒（仅当提醒处于Scheduled状态时可用）
    /// </summary>
    Task PauseAsync();
    
    /// <summary>
    /// 恢复提醒（仅当提醒处于Paused状态时可用）
    /// </summary>
    Task ResumeAsync();
    
    /// <summary>
    /// 删除提醒及其所有状态数据
    /// </summary>
    Task DeleteAsync();
}
