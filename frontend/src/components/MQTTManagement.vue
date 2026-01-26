<template>
  <div class="mqtt-management">
    <el-row :gutter="20">
      <el-col :span="8">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>MQTT连接</span>
              <el-button :type="connected ? 'danger' : 'primary'" size="small" @click="toggleConnection">
                {{ connected ? '断开连接' : '连接' }}
              </el-button>
            </div>
          </template>
          <el-form :model="connectionConfig" label-width="100px" size="small">
            <el-form-item label="服务器地址">
              <el-input v-model="connectionConfig.host" :disabled="connected" />
            </el-form-item>
            <el-form-item label="端口">
              <el-input-number v-model="connectionConfig.port" :min="1" :max="65535" :disabled="connected" />
            </el-form-item>
            <el-form-item label="客户端ID">
              <el-input v-model="connectionConfig.clientId" :disabled="connected" />
            </el-form-item>
            <el-form-item label="用户名">
              <el-input v-model="connectionConfig.username" :disabled="connected" />
            </el-form-item>
            <el-form-item label="密码">
              <el-input v-model="connectionConfig.password" type="password" show-password :disabled="connected" />
            </el-form-item>
            <el-form-item label="连接状态">
              <el-tag :type="connected ? 'success' : 'info'">
                {{ connected ? '已连接' : '未连接' }}
              </el-tag>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card style="margin-top: 20px">
          <template #header>
            <div class="card-header">
              <span>主题订阅</span>
              <el-button type="primary" size="small" :icon="Plus" @click="openSubscribeDialog">
                订阅主题
              </el-button>
            </div>
          </template>
          <el-table :data="subscriptions" style="width: 100%" size="small">
            <el-table-column prop="topic" label="主题" />
            <el-table-column prop="qos" label="QoS" width="60" />
            <el-table-column label="操作" width="80">
              <template #default="{ row }">
                <el-button size="small" type="danger" :icon="Delete" @click="unsubscribe(row)" />
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>

      <el-col :span="16">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>消息发布</span>
            </div>
          </template>
          <el-form :model="publishForm" label-width="80px" size="small">
            <el-form-item label="主题">
              <el-input v-model="publishForm.topic" placeholder="请输入主题" />
            </el-form-item>
            <el-form-item label="QoS">
              <el-radio-group v-model="publishForm.qos">
                <el-radio :label="0">0</el-radio>
                <el-radio :label="1">1</el-radio>
                <el-radio :label="2">2</el-radio>
              </el-radio-group>
            </el-form-item>
            <el-form-item label="保留">
              <el-switch v-model="publishForm.retain" />
            </el-form-item>
            <el-form-item label="消息内容">
              <el-input
                v-model="publishForm.payload"
                type="textarea"
                :rows="4"
                placeholder="请输入消息内容（支持JSON格式）"
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" @click="publishMessage" :disabled="!connected">
                发布消息
              </el-button>
              <el-button @click="clearPublishForm">清空</el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card style="margin-top: 20px">
          <template #header>
            <div class="card-header">
              <span>接收消息</span>
              <el-button size="small" @click="clearMessages">清空</el-button>
            </div>
          </template>
          <div class="message-container">
            <div v-for="(msg, index) in messages" :key="index" class="message-item">
              <div class="message-header">
                <span class="message-topic">{{ msg.topic }}</span>
                <span class="message-time">{{ msg.time }}</span>
                <el-tag size="small" :type="msg.qos === 0 ? 'info' : 'success'">QoS: {{ msg.qos }}</el-tag>
              </div>
              <div class="message-content">{{ msg.payload }}</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-dialog v-model="subscribeDialogVisible" title="订阅主题" width="500px">
      <el-form :model="subscribeForm" label-width="80px">
        <el-form-item label="主题">
          <el-input v-model="subscribeForm.topic" placeholder="例如: device/+/data" />
        </el-form-item>
        <el-form-item label="QoS">
          <el-radio-group v-model="subscribeForm.qos">
            <el-radio :label="0">0</el-radio>
            <el-radio :label="1">1</el-radio>
            <el-radio :label="2">2</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="subscribeDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="subscribe">订阅</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="deviceDialogVisible" title="MQTT设备管理" width="800px">
      <el-table :data="devices" style="width: 100%">
        <el-table-column prop="clientId" label="客户端ID" width="200" />
        <el-table-column prop="ipAddress" label="IP地址" width="150" />
        <el-table-column label="连接时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.connectTime) }}
          </template>
        </el-table-column>
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.online ? 'success' : 'info'" size="small">
              {{ row.online ? '在线' : '离线' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="150">
          <template #default="{ row }">
            <el-button size="small" @click="viewDeviceDetails(row)">详情</el-button>
            <el-button size="small" type="danger" @click="disconnectDevice(row)">断开</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-dialog>

    <el-dialog v-model="taskTriggerDialogVisible" title="MQTT任务触发配置" width="600px">
      <el-form :model="taskTriggerForm" label-width="120px">
        <el-form-item label="触发主题">
          <el-input v-model="taskTriggerForm.topic" placeholder="例如: task/trigger" />
        </el-form-item>
        <el-form-item label="任务ID">
          <el-input v-model="taskTriggerForm.taskId" placeholder="请输入任务ID" />
        </el-form-item>
        <el-form-item label="操作类型">
          <el-select v-model="taskTriggerForm.action" placeholder="请选择操作类型">
            <el-option label="启动任务" value="start" />
            <el-option label="停止任务" value="stop" />
            <el-option label="重启任务" value="restart" />
          </el-select>
        </el-form-item>
        <el-form-item label="消息格式">
          <el-input
            v-model="taskTriggerForm.messageFormat"
            type="textarea"
            :rows="3"
            placeholder='例如: {"taskId": "task-1", "action": "start"}'
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="taskTriggerDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveTaskTrigger">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Plus, Delete } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'

interface ConnectionConfig {
  host: string
  port: number
  clientId: string
  username: string
  password: string
}

interface Subscription {
  topic: string
  qos: number
}

interface Message {
  topic: string
  qos: number
  payload: string
  time: string
}

interface Device {
  clientId: string
  ipAddress: string
  connectTime: string
  online: boolean
}

const connected = ref(false)
const connectionConfig = ref<ConnectionConfig>({
  host: '192.168.91.128',
  port: 1883,
  clientId: 'mcs-client-' + Math.random().toString(16).substr(2, 8),
  username: '',
  password: ''
})

const subscriptions = ref<Subscription[]>([
  { topic: 'device/+/data', qos: 0 },
  { topic: 'task/trigger', qos: 1 },
  { topic: 'system/status', qos: 0 }
])

const publishForm = ref({
  topic: '',
  qos: 0,
  retain: false,
  payload: ''
})

const messages = ref<Message[]>([
  {
    topic: 'device/sensor1/data',
    qos: 0,
    payload: '{"temperature": 25.5, "humidity": 60, "timestamp": "2024-01-24T10:30:00Z"}',
    time: '10:30:00'
  },
  {
    topic: 'task/trigger',
    qos: 1,
    payload: '{"taskId": "task-1", "action": "start"}',
    time: '10:28:00'
  },
  {
    topic: 'system/status',
    qos: 0,
    payload: '{"status": "online", "uptime": 3600}',
    time: '10:25:00'
  }
])

const devices = ref<Device[]>([
  {
    clientId: 'device-sensor-001',
    ipAddress: '192.168.1.101',
    connectTime: '2024-01-24T08:00:00Z',
    online: true
  },
  {
    clientId: 'device-sensor-002',
    ipAddress: '192.168.1.102',
    connectTime: '2024-01-24T09:00:00Z',
    online: true
  },
  {
    clientId: 'device-controller-001',
    ipAddress: '192.168.1.103',
    connectTime: '2024-01-24T07:30:00Z',
    online: false
  }
])

const subscribeDialogVisible = ref(false)
const deviceDialogVisible = ref(false)
const taskTriggerDialogVisible = ref(false)

const subscribeForm = ref({
  topic: '',
  qos: 0
})

const taskTriggerForm = ref({
  topic: 'task/trigger',
  taskId: '',
  action: 'start',
  messageFormat: '{"taskId": "task-1", "action": "start"}'
})

let messageInterval: any

onMounted(() => {
  simulateMessages()
})

onUnmounted(() => {
  if (messageInterval) {
    clearInterval(messageInterval)
  }
})

function toggleConnection() {
  if (connected.value) {
    connected.value = false
    ElMessage.success('MQTT连接已断开')
  } else {
    connected.value = true
    ElMessage.success('MQTT连接成功')
  }
}

function openSubscribeDialog() {
  subscribeForm.value = {
    topic: '',
    qos: 0
  }
  subscribeDialogVisible.value = true
}

function subscribe() {
  if (!subscribeForm.value.topic) {
    ElMessage.warning('请输入主题')
    return
  }
  subscriptions.value.push({
    topic: subscribeForm.value.topic,
    qos: subscribeForm.value.qos
  })
  subscribeDialogVisible.value = false
  ElMessage.success('订阅成功')
}

function unsubscribe(row: Subscription) {
  const index = subscriptions.value.findIndex(s => s.topic === row.topic)
  if (index !== -1) {
    subscriptions.value.splice(index, 1)
    ElMessage.success('取消订阅成功')
  }
}

function publishMessage() {
  if (!publishForm.value.topic) {
    ElMessage.warning('请输入主题')
    return
  }
  if (!publishForm.value.payload) {
    ElMessage.warning('请输入消息内容')
    return
  }

  const newMessage: Message = {
    topic: publishForm.value.topic,
    qos: publishForm.value.qos,
    payload: publishForm.value.payload,
    time: new Date().toLocaleTimeString()
  }

  messages.value.unshift(newMessage)
  if (messages.value.length > 100) {
    messages.value.pop()
  }

  ElMessage.success('消息发布成功')
}

function clearPublishForm() {
  publishForm.value = {
    topic: '',
    qos: 0,
    retain: false,
    payload: ''
  }
}

function clearMessages() {
  messages.value = []
  ElMessage.success('消息已清空')
}

function formatDate(dateString: string) {
  const date = new Date(dateString)
  return date.toLocaleString('zh-CN')
}

function viewDeviceDetails(row: Device) {
  ElMessage.info(`设备详情: ${row.clientId}`)
}

function disconnectDevice(row: Device) {
  const index = devices.value.findIndex(d => d.clientId === row.clientId)
  if (index !== -1) {
    devices.value[index].online = false
    ElMessage.success('设备已断开连接')
  }
}

function saveTaskTrigger() {
  ElMessage.success('MQTT任务触发配置已保存')
  taskTriggerDialogVisible.value = false
}

function simulateMessages() {
  messageInterval = setInterval(() => {
    if (connected.value) {
      const topics = ['device/sensor1/data', 'device/sensor2/data', 'system/status']
      const randomTopic = topics[Math.floor(Math.random() * topics.length)]
      
      const newMessage: Message = {
        topic: randomTopic,
        qos: Math.floor(Math.random() * 2),
        payload: JSON.stringify({
          value: Math.random() * 100,
          timestamp: new Date().toISOString()
        }),
        time: new Date().toLocaleTimeString()
      }

      messages.value.unshift(newMessage)
      if (messages.value.length > 100) {
        messages.value.pop()
      }
    }
  }, 10000)
}
</script>

<style scoped>
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.message-container {
  height: 400px;
  overflow-y: auto;
  background: #f5f5f5;
  padding: 10px;
  border-radius: 4px;
}

.message-item {
  background: #fff;
  padding: 10px;
  margin-bottom: 10px;
  border-radius: 4px;
  border-left: 3px solid #409eff;
}

.message-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 5px;
}

.message-topic {
  font-weight: bold;
  color: #303133;
}

.message-time {
  font-size: 12px;
  color: #909399;
}

.message-content {
  font-family: 'Courier New', monospace;
  font-size: 12px;
  color: #606266;
  word-break: break-all;
  background: #f5f5f5;
  padding: 5px;
  border-radius: 3px;
}
</style>
