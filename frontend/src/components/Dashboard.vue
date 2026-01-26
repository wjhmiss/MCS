<template>
  <div class="dashboard">
    <el-row :gutter="20">
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <div class="stat-icon" style="background: #409eff">
              <el-icon><List /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ stats.totalTasks }}</div>
              <div class="stat-label">总任务数</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <div class="stat-icon" style="background: #67c23a">
              <el-icon><Connection /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ stats.totalWorkflows }}</div>
              <div class="stat-label">总工作流数</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <div class="stat-icon" style="background: #e6a23c">
              <el-icon><Timer /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ stats.runningExecutions }}</div>
              <div class="stat-label">运行中</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <div class="stat-icon" style="background: #f56c6c">
              <el-icon><Bell /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ stats.activeAlerts }}</div>
              <div class="stat-label">告警数</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="20" style="margin-top: 20px">
      <el-col :span="8">
        <el-card>
          <template #header>
            <span>任务执行统计</span>
          </template>
          <div class="chart-container">
            <div class="chart-item">
              <div class="chart-bar">
                <div class="bar-fill success" :style="{ width: taskStats.successRate + '%' }"></div>
              </div>
              <div class="chart-label">
                <span>成功率</span>
                <span class="chart-value">{{ taskStats.successRate }}%</span>
              </div>
            </div>
            <div class="chart-item">
              <div class="chart-bar">
                <div class="bar-fill warning" :style="{ width: taskStats.failureRate + '%' }"></div>
              </div>
              <div class="chart-label">
                <span>失败率</span>
                <span class="chart-value">{{ taskStats.failureRate }}%</span>
              </div>
            </div>
            <div class="chart-item">
              <div class="chart-bar">
                <div class="bar-fill info" :style="{ width: taskStats.pendingRate + '%' }"></div>
              </div>
              <div class="chart-label">
                <span>待处理</span>
                <span class="chart-value">{{ taskStats.pendingRate }}%</span>
              </div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card>
          <template #header>
            <span>定时任务状态</span>
          </template>
          <div class="schedule-stats">
            <div class="schedule-item">
              <div class="schedule-icon" style="background: #67c23a">
                <el-icon><CircleCheck /></el-icon>
              </div>
              <div class="schedule-info">
                <div class="schedule-value">{{ scheduleStats.enabled }}</div>
                <div class="schedule-label">已启用</div>
              </div>
            </div>
            <div class="schedule-item">
              <div class="schedule-icon" style="background: #909399">
                <el-icon><CircleClose /></el-icon>
              </div>
              <div class="schedule-info">
                <div class="schedule-value">{{ scheduleStats.disabled }}</div>
                <div class="schedule-label">已禁用</div>
              </div>
            </div>
            <div class="schedule-item">
              <div class="schedule-icon" style="background: #409eff">
                <el-icon><Clock /></el-icon>
              </div>
              <div class="schedule-info">
                <div class="schedule-value">{{ scheduleStats.todayRuns }}</div>
                <div class="schedule-label">今日执行</div>
              </div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card>
          <template #header>
            <span>MQTT连接状态</span>
          </template>
          <div class="mqtt-stats">
            <div class="mqtt-status" :class="{ connected: mqttStatus.connected }">
              <el-icon :size="40">
                <component :is="mqttStatus.connected ? 'Connection' : 'CircleClose'" />
              </el-icon>
              <div class="mqtt-status-text">
                {{ mqttStatus.connected ? '已连接' : '未连接' }}
              </div>
            </div>
            <div class="mqtt-info">
              <div class="mqtt-info-item">
                <span>订阅主题:</span>
                <span>{{ mqttStatus.subscriptions }}</span>
              </div>
              <div class="mqtt-info-item">
                <span>消息接收:</span>
                <span>{{ mqttStatus.receivedMessages }}</span>
              </div>
              <div class="mqtt-info-item">
                <span>消息发送:</span>
                <span>{{ mqttStatus.sentMessages }}</span>
              </div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="20" style="margin-top: 20px">
      <el-col :span="12">
        <el-card>
          <template #header>
            <span>最近执行</span>
          </template>
          <el-table :data="recentExecutions" style="width: 100%" size="small">
            <el-table-column prop="id" label="ID" width="60" />
            <el-table-column prop="type" label="类型" width="70">
              <template #default="{ row }">
                <el-tag :type="row.type === '任务' ? 'primary' : 'success'" size="small">
                  {{ row.type }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="name" label="名称" width="120" show-overflow-tooltip />
            <el-table-column prop="status" label="状态" width="80">
              <template #default="{ row }">
                <el-tag :type="getStatusTagType(row.status)" size="small">
                  {{ row.status }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="duration" label="耗时" width="80" />
            <el-table-column prop="time" label="时间" />
          </el-table>
        </el-card>
      </el-col>
      <el-col :span="12">
        <el-card>
          <template #header>
            <span>系统状态</span>
          </template>
          <el-descriptions :column="1" border size="small">
            <el-descriptions-item label="Orleans集群">
              <el-tag type="success" size="small">运行中</el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="数据库连接">
              <el-tag type="success" size="small">正常</el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="Redis连接">
              <el-tag type="success" size="small">正常</el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="MQTT连接">
              <el-tag :type="mqttStatus.connected ? 'success' : 'info'" size="small">
                {{ mqttStatus.connected ? '正常' : '断开' }}
              </el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="CPU使用率">
              <el-progress :percentage="systemStats.cpuUsage" :color="getProgressColor(systemStats.cpuUsage)" />
            </el-descriptions-item>
            <el-descriptions-item label="内存使用率">
              <el-progress :percentage="systemStats.memoryUsage" :color="getProgressColor(systemStats.memoryUsage)" />
            </el-descriptions-item>
            <el-descriptions-item label="运行时间">
              {{ formatUptime(systemStats.uptime) }}
            </el-descriptions-item>
          </el-descriptions>
        </el-card>
      </el-col>
    </el-row>

    <el-row style="margin-top: 20px">
      <el-col :span="24">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>实时日志</span>
              <el-button size="small" @click="clearLogs">清空</el-button>
            </div>
          </template>
          <div class="log-container">
            <div v-for="(log, index) in logs" :key="index" class="log-item">
              <span class="log-time">{{ log.time }}</span>
              <span class="log-level" :class="'log-' + log.level.toLowerCase()">{{ log.level }}</span>
              <span class="log-source">{{ log.source }}</span>
              <span class="log-message">{{ log.message }}</span>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { List, Connection, Timer, Bell, CircleCheck, CircleClose, Clock } from '@element-plus/icons-vue'

const stats = ref({
  totalTasks: 15,
  totalWorkflows: 8,
  runningExecutions: 3,
  activeAlerts: 2
})

const taskStats = ref({
  successRate: 85,
  failureRate: 10,
  pendingRate: 5
})

const scheduleStats = ref({
  enabled: 5,
  disabled: 2,
  todayRuns: 24
})

const mqttStatus = ref({
  connected: true,
  subscriptions: 8,
  receivedMessages: 156,
  sentMessages: 89
})

const systemStats = ref({
  cpuUsage: 15,
  memoryUsage: 45,
  uptime: 3600
})

const recentExecutions = ref([
  { id: '1', type: '任务', name: '数据同步任务', status: 'Completed', duration: '2.3s', time: '10:30:00' },
  { id: '2', type: '工作流', name: '备份工作流', status: 'Running', duration: '-', time: '10:28:00' },
  { id: '3', type: '任务', name: 'API调用任务', status: 'Failed', duration: '5.0s', time: '10:25:00' },
  { id: '4', type: '工作流', name: '数据处理工作流', status: 'Completed', duration: '8.7s', time: '10:20:00' },
  { id: '5', type: '任务', name: 'MQTT发布任务', status: 'Completed', duration: '0.5s', time: '10:15:00' }
])

const logs = ref([
  { time: '10:30:05', level: 'INFO', source: 'Task', message: '任务 task-1 执行成功' },
  { time: '10:30:00', level: 'INFO', source: 'Workflow', message: '工作流 workflow-1 开始执行' },
  { time: '10:28:15', level: 'WARN', source: 'Task', message: '任务 task-2 执行超时，正在重试' },
  { time: '10:25:30', level: 'ERROR', source: 'API', message: 'API调用失败: Connection timeout' },
  { time: '10:20:00', level: 'INFO', source: 'Schedule', message: '定时任务触发: schedule-1' },
  { time: '10:18:45', level: 'INFO', source: 'MQTT', message: '接收到消息: device/sensor1/data' },
  { time: '10:15:00', level: 'INFO', source: 'Alert', message: '告警已解决: alert-1' }
])

let logInterval: any
let statsInterval: any

onMounted(() => {
  logInterval = setInterval(() => {
    const sources = ['Task', 'Workflow', 'API', 'MQTT', 'Schedule', 'Alert', 'System']
    const levels = ['INFO', 'INFO', 'INFO', 'WARN', 'ERROR']
    const messages = [
      '任务执行成功',
      '工作流开始执行',
      'API调用完成',
      'MQTT消息接收',
      '定时任务触发',
      '告警已处理',
      '系统正常运行'
    ]
    
    const newLog = {
      time: new Date().toLocaleTimeString(),
      level: levels[Math.floor(Math.random() * levels.length)],
      source: sources[Math.floor(Math.random() * sources.length)],
      message: messages[Math.floor(Math.random() * messages.length)]
    }
    logs.value.unshift(newLog)
    if (logs.value.length > 50) {
      logs.value.pop()
    }
  }, 5000)

  statsInterval = setInterval(() => {
    systemStats.value.cpuUsage = Math.floor(Math.random() * 30) + 10
    systemStats.value.memoryUsage = Math.floor(Math.random() * 20) + 40
    systemStats.value.uptime += 5
  }, 5000)
})

onUnmounted(() => {
  if (logInterval) {
    clearInterval(logInterval)
  }
  if (statsInterval) {
    clearInterval(statsInterval)
  }
})

function getStatusTagType(status: string) {
  const types: Record<string, any> = {
    Completed: 'success',
    Running: 'primary',
    Failed: 'danger',
    Pending: 'info'
  }
  return types[status] || ''
}

function getProgressColor(percentage: number) {
  if (percentage < 50) return '#67c23a'
  if (percentage < 80) return '#e6a23c'
  return '#f56c6c'
}

function formatUptime(seconds: number) {
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  const secs = seconds % 60
  return `${hours}小时 ${minutes}分钟 ${secs}秒`
}

function clearLogs() {
  logs.value = []
}
</script>

<style scoped>
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 20px;
}

.stat-icon {
  width: 60px;
  height: 60px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 24px;
}

.stat-content {
  flex: 1;
}

.stat-value {
  font-size: 28px;
  font-weight: bold;
  color: #303133;
}

.stat-label {
  font-size: 14px;
  color: #909399;
  margin-top: 5px;
}

.chart-container {
  padding: 10px 0;
}

.chart-item {
  margin-bottom: 20px;
}

.chart-bar {
  height: 20px;
  background: #f0f0f0;
  border-radius: 10px;
  overflow: hidden;
  margin-bottom: 8px;
}

.bar-fill {
  height: 100%;
  border-radius: 10px;
  transition: width 0.3s;
}

.bar-fill.success {
  background: #67c23a;
}

.bar-fill.warning {
  background: #e6a23c;
}

.bar-fill.info {
  background: #409eff;
}

.chart-label {
  display: flex;
  justify-content: space-between;
  font-size: 14px;
}

.chart-value {
  font-weight: bold;
  color: #303133;
}

.schedule-stats {
  display: flex;
  flex-direction: column;
  gap: 15px;
}

.schedule-item {
  display: flex;
  align-items: center;
  gap: 15px;
}

.schedule-icon {
  width: 50px;
  height: 50px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 24px;
}

.schedule-info {
  flex: 1;
}

.schedule-value {
  font-size: 24px;
  font-weight: bold;
  color: #303133;
}

.schedule-label {
  font-size: 12px;
  color: #909399;
}

.mqtt-stats {
  text-align: center;
}

.mqtt-status {
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-bottom: 20px;
}

.mqtt-status.connected {
  color: #67c23a;
}

.mqtt-status:not(.connected) {
  color: #909399;
}

.mqtt-status-text {
  font-size: 18px;
  font-weight: bold;
  margin-top: 10px;
}

.mqtt-info {
  text-align: left;
}

.mqtt-info-item {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  border-bottom: 1px solid #f0f0f0;
}

.mqtt-info-item:last-child {
  border-bottom: none;
}

.mqtt-info-item span:first-child {
  color: #909399;
}

.mqtt-info-item span:last-child {
  font-weight: bold;
  color: #303133;
}

.log-container {
  height: 300px;
  overflow-y: auto;
  background: #f5f5f5;
  padding: 10px;
  border-radius: 4px;
}

.log-item {
  padding: 5px 0;
  border-bottom: 1px solid #e0e0e0;
  font-family: 'Courier New', monospace;
  font-size: 12px;
}

.log-time {
  color: #909399;
  margin-right: 10px;
}

.log-level {
  display: inline-block;
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 10px;
  margin-right: 10px;
  font-weight: bold;
  min-width: 40px;
  text-align: center;
}

.log-info {
  background: #e1f3d8;
  color: #67c23a;
}

.log-warn {
  background: #faecd8;
  color: #e6a23c;
}

.log-error {
  background: #fde2e2;
  color: #f56c6c;
}

.log-source {
  color: #409eff;
  margin-right: 10px;
  min-width: 60px;
  display: inline-block;
}

.log-message {
  color: #303133;
}
</style>
