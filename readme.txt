## 推荐技术栈
### 后端
核心框架：.NET 8 + Orleans 8.x + sqlsugar 框架 + 集成swagger查看接口。
功能要求：开发一个工作流管理系统，功能如下：

1，可以创建工作流和任务，一个工作流可以由一个或多个任务组成。任务不能单独启动和执行，只有工作流可以。每个任务可以通过API和MQTT与外部设备进行通信，可以调用和接收外部API和发布订阅外部MQTT。

2，一个工作流可以设置自动循环执行，设置定时启动，也可以由外部调用API或mqtt等启动和终止。

3，一个工作流中，可以设置任务并联和串联执行。

4，一个工作流中，上一个任务可以根据外部API和外部MQTT，本程序命令跳到指定的后续任务中继续执行，也可以终止后面的所有任务。

5，一个工作流中，任务执行时间可以长时间等待，直到满足上个任务通过后，再执行下一个任务。

6，需要设计合理的可视化界面，包括：工作流创建，查看当前正在执行的工作流中详细任务，查看工作流历史记录等等，用vue3 setup开发。

7，后续需要部署到K8S 容器中。

需满足以上功能，需要合理设计表及表字段。

技术选型如下：

1，存储方案：
- PostgreSQL - Orleans Storage（任务状态持久化）
- Redis - 缓存和分布式锁

postgres 链接地址：192.168.91.128 端口：5432   密码：password.123，需新建数据库MCS
redis地址：192.168.91.128 端口：6379   密码：password.123
MQTT地址：192.168.91.128 MQTT 监听器(端口1883),WebSocket 监听器（端口 9001）

2，消息通信：
- MQTTnet - 外部触发和设备通信
- Orleans Streams - 内部事件流转

### 前端
框架：Vue 3 + TypeScript + Vite

核心组件：
- Element Plus - UI 组件库
- AntV X6 - 流程图可视化编辑器
- Pinia - 状态管理
- SignalR Client - 实时通信
### 部署
容器化：Kubernetes

服务组件：

- Orleans Silo Cluster - 分布式任务执行集群
- PostgreSQL - 数据库
- Redis - 缓存服务
- MQTT Broker - Mosquitto 或 EMQX
- K8S 集群



