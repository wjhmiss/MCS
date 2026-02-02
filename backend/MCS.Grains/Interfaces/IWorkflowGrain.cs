using Orleans;
using MCS.Grains.Models;
using ModelsTaskType = MCS.Grains.Models.TaskType;

namespace MCS.Grains.Interfaces;

/// <summary>
/// 工作流Grain接口
/// 定义工作流的所有操作方法，包括创建、任务管理、执行控制、状态查询等
/// </summary>
public interface IWorkflowGrain : IGrainWithStringKey
{
    /// <summary>
    /// 创建工作流
    /// 初始化工作流状态，支持设置定时执行参数
    /// </summary>
    /// <param name="name">工作流名称</param>
    /// <param name="scheduledTime">首次执行时间（可选，null表示立即执行）</param>
    /// <param name="period">循环周期（可选，null表示一次性执行）</param>
    /// <param name="maxExecutions">最大执行次数（可选，null表示无限循环）</param>
    /// <returns>工作流ID</returns>
    Task<string> CreateWorkflowAsync(string name, DateTime? scheduledTime = null, TimeSpan? period = null, int? maxExecutions = null);

    /// <summary>
    /// 批量添加、编辑或删除任务到工作流
    /// 传入的任务列表是整个工作流的任务列表
    /// 当任务存在时更新任务信息，当任务不存在时添加新任务
    /// 之前的工作流中的任务如果不在传入的任务列表内，则删除
    /// </summary>
    /// <param name="tasks">任务列表，每个任务包含taskId、name、type、order、data</param>
    /// <returns>任务ID列表</returns>
    Task<List<string>> AddAndEditTasksAsync(List<(string taskId, string name, ModelsTaskType type, int order, Dictionary<string, object>? data)> tasks);

    /// <summary>
    /// 启动工作流
    /// 开始按顺序执行任务
    /// 如果设置了定时执行参数，则注册提醒
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 暂停工作流
    /// 暂停当前正在执行的任务
    /// 如果工作流有定时执行配置，也暂停定时执行
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// 继续工作流
    /// 从暂停的位置继续执行任务
    /// 如果工作流有定时执行配置，也恢复定时执行
    /// </summary>
    Task ResumeAsync();

    /// <summary>
    /// 停止工作流
    /// 停止当前正在执行的任务，并取消所有后续任务
    /// 如果工作流有定时执行配置，也取消定时执行
    /// 停止后只能重新开始，不能继续
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 获取工作流状态
    /// 返回工作流的完整状态信息
    /// </summary>
    /// <returns>工作流状态对象</returns>
    Task<WorkflowState> GetStateAsync();

    /// <summary>
    /// 获取工作流中的所有任务
    /// 从TaskGrain中获取完整的任务状态信息
    /// </summary>
    /// <returns>任务状态列表</returns>
    Task<List<TaskState>> GetTasksAsync();

    /// <summary>
    /// 接收任务完成通知
    /// 由TaskGrain在任务完成或失败时调用
    /// 更新任务状态，并根据结果决定是否继续执行下一个任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="success">任务是否成功完成</param>
    /// <param name="errorMessage">任务失败时的错误信息</param>
    Task NotifyTaskCompletedAsync(string taskId, bool success, string? errorMessage = null);
}
