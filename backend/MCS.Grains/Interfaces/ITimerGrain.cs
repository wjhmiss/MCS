using Orleans;
using MCS.Grains.Models;

namespace MCS.Grains.Interfaces;

/// <summary>
/// 定时器Grain接口，用于创建和管理内存中的定时任务
/// 支持高精度定时、暂停恢复、动态调整间隔等功能
/// </summary>
public interface ITimerGrain : IGrainWithStringKey
{
    /// <summary>
    /// 创建一个新的定时器
    /// </summary>
    /// <param name="name">定时器名称</param>
    /// <param name="interval">执行间隔时间</param>
    /// <param name="data">自定义数据字典</param>
    /// <param name="maxExecutions">最大执行次数（null表示无限执行）</param>
    /// <returns>定时器ID</returns>
    Task<string> CreateTimerAsync(string name, TimeSpan interval, Dictionary<string, object>? data = null, int? maxExecutions = null);
    
    /// <summary>
    /// 获取定时器的完整状态
    /// </summary>
    /// <returns>定时器状态对象</returns>
    Task<TimerState> GetStateAsync();
    
    /// <summary>
    /// 启动定时器（仅当定时器处于Stopped状态时可用）
    /// </summary>
    Task StartAsync();
    
    /// <summary>
    /// 暂停定时器（仅当定时器处于Active状态时可用）
    /// </summary>
    Task PauseAsync();
    
    /// <summary>
    /// 恢复定时器（仅当定时器处于Paused状态时可用）
    /// </summary>
    Task ResumeAsync();
    
    /// <summary>
    /// 停止定时器
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// 获取定时器的执行日志
    /// </summary>
    /// <returns>执行日志列表</returns>
    Task<List<string>> GetExecutionLogsAsync();
    
    /// <summary>
    /// 获取定时器的当前状态
    /// </summary>
    /// <returns>定时器状态枚举</returns>
    Task<TimerStatus> GetStatusAsync();
    
    /// <summary>
    /// 更新定时器的执行间隔
    /// </summary>
    /// <param name="newInterval">新的执行间隔</param>
    Task UpdateIntervalAsync(TimeSpan newInterval);
    
    /// <summary>
    /// 删除定时器及其所有状态数据
    /// </summary>
    Task DeleteAsync();
}
