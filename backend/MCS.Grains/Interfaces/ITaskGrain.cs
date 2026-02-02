using Orleans;
using MCS.Grains.Models;
using ModelsTaskType = MCS.Grains.Models.TaskType;

namespace MCS.Grains.Interfaces;

/// <summary>
/// 任务Grain接口
/// 定义任务的所有操作方法，包括初始化、更新、执行、暂停、继续、停止等
/// 支持两种任务类型：Direct（直接执行）和WaitForExternal（等待外部指令）
/// </summary>
public interface ITaskGrain : IGrainWithStringKey
{
    /// <summary>
    /// 初始化任务
    /// 设置任务的基本信息和初始状态
    /// 由WorkflowGrain在添加任务时调用
    /// </summary>
    /// <param name="workflowId">所属工作流的ID</param>
    /// <param name="name">任务名称</param>
    /// <param name="type">任务类型（Direct或WaitForExternal）</param>
    /// <param name="order">任务在工作流中的执行顺序</param>
    /// <param name="data">任务的自定义数据字典</param>
    Task InitializeAsync(string workflowId, string name, ModelsTaskType type, int order, Dictionary<string, object>? data = null);

    /// <summary>
    /// 更新任务信息
    /// 更新任务的名称、类型、顺序和自定义数据
    /// 只能在Pending状态下更新任务
    /// </summary>
    /// <param name="name">任务名称</param>
    /// <param name="type">任务类型（Direct或WaitForExternal）</param>
    /// <param name="order">任务在工作流中的执行顺序</param>
    /// <param name="data">任务的自定义数据字典</param>
    Task UpdateAsync(string name, ModelsTaskType type, int order, Dictionary<string, object>? data = null);

    /// <summary>
    /// 执行任务
    /// 根据任务类型执行不同的逻辑
    /// Direct类型：立即执行并完成
    /// WaitForExternal类型：等待外部指令后才能完成
    /// </summary>
    Task ExecuteAsync();

    /// <summary>
    /// 暂停任务
    /// 将任务状态从Running或WaitingForExternal改为Pending
    /// 只能在Running或WaitingForExternal状态下暂停
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// 继续任务
    /// 从暂停的位置继续执行任务
    /// 只能在Pending状态下继续
    /// </summary>
    Task ResumeAsync();

    /// <summary>
    /// 停止任务
    /// 将任务状态更新为已取消
    /// 已完成、失败或取消的任务不能再次停止
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 接收外部指令
    /// 由外部系统调用，通知等待外部指令的任务可以继续执行
    /// 只能在WaitingForExternal状态下调用
    /// </summary>
    Task NotifyExternalCommandAsync();

    /// <summary>
    /// 获取任务状态
    /// 返回任务的完整状态信息
    /// </summary>
    /// <returns>任务状态对象</returns>
    Task<TaskState> GetStateAsync();
}
