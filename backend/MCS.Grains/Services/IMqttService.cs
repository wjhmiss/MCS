namespace MCS.Grains.Services;

/// <summary>
/// MQTT 服务接口
/// 用于发布和订阅 MQTT 消息
/// </summary>
public interface IMqttService
{
    /// <summary>
    /// 发布 MQTT 消息
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="message">消息内容</param>
    /// <param name="retain">是否保留消息</param>
    /// <param name="qos">服务质量等级</param>
    Task PublishAsync(string topic, string message, bool retain = false, int qos = 0);

    /// <summary>
    /// 订阅 MQTT 主题
    /// </summary>
    /// <param name="topic">主题</param>
    /// <param name="callback">消息回调</param>
    Task SubscribeAsync(string topic, Func<string, string, Task> callback);

    /// <summary>
    /// 取消订阅 MQTT 主题
    /// </summary>
    /// <param name="topic">主题</param>
    Task UnsubscribeAsync(string topic);

    /// <summary>
    /// 连接 MQTT 服务器
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// 断开 MQTT 服务器连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 检查连接状态
    /// </summary>
    bool IsConnected { get; }
}