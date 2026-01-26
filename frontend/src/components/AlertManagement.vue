<template>
  <div class="alert-management">
    <el-row :gutter="20">
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <div class="stat-icon" style="background: #f56c6c">
              <el-icon><Warning /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ alertStats.total }}</div>
              <div class="stat-label">总告警数</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <div class="stat-icon" style="background: #e6a23c">
              <el-icon><Clock /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ alertStats.pending }}</div>
              <div class="stat-label">待处理</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <div class="stat-icon" style="background: #67c23a">
              <el-icon><CircleCheck /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ alertStats.resolved }}</div>
              <div class="stat-label">已解决</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <div class="stat-icon" style="background: #909399">
              <el-icon><CircleClose /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ alertStats.ignored }}</div>
              <div class="stat-label">已忽略</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="20" style="margin-top: 20px">
      <el-col :span="16">
        <el-card>
          <template #header>
            <div class="card-header">
              <span>告警列表</span>
              <div>
                <el-button type="primary" size="small" :icon="Plus" @click="openCreateRuleDialog">
                  新建告警规则
                </el-button>
                <el-button size="small" @click="loadAlerts">刷新</el-button>
              </div>
            </div>
          </template>

          <el-tabs v-model="activeTab" @tab-click="handleTabClick">
            <el-tab-pane label="全部" name="all" />
            <el-tab-pane label="待处理" name="pending" />
            <el-tab-pane label="已解决" name="resolved" />
            <el-tab-pane label="已忽略" name="ignored" />
          </el-tabs>

          <el-table :data="filteredAlerts" style="width: 100%" v-loading="loading">
            <el-table-column prop="id" label="ID" width="80" />
            <el-table-column prop="ruleName" label="告警规则" width="150" />
            <el-table-column prop="level" label="级别" width="100">
              <template #default="{ row }">
                <el-tag :type="getLevelTagType(row.level)" size="small">
                  {{ row.level }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="source" label="来源" width="120" />
            <el-table-column prop="message" label="告警内容" show-overflow-tooltip />
            <el-table-column label="触发时间" width="180">
              <template #default="{ row }">
                {{ formatDate(row.triggerTime) }}
              </template>
            </el-table-column>
            <el-table-column label="状态" width="100">
              <template #default="{ row }">
                <el-tag :type="getStatusTagType(row.status)" size="small">
                  {{ row.status }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="200" fixed="right">
              <template #default="{ row }">
                <el-button size="small" type="success" :icon="CircleCheck" @click="resolveAlert(row)">
                  解决
                </el-button>
                <el-button size="small" :icon="View" @click="viewAlertDetails(row)">详情</el-button>
                <el-button size="small" type="danger" :icon="Delete" @click="deleteAlert(row)">
                  删除
                </el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>

      <el-col :span="8">
        <el-card>
          <template #header>
            <span>告警规则</span>
          </template>
          <el-table :data="alertRules" style="width: 100%" size="small">
            <el-table-column prop="name" label="规则名称" width="120" />
            <el-table-column prop="level" label="级别" width="60">
              <template #default="{ row }">
                <el-tag :type="getLevelTagType(row.level)" size="small">
                  {{ row.level }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="启用" width="60">
              <template #default="{ row }">
                <el-switch v-model="row.enabled" @change="toggleRule(row)" />
              </template>
            </el-table-column>
            <el-table-column label="操作" width="80">
              <template #default="{ row }">
                <el-button size="small" :icon="Edit" @click="editRule(row)" />
              </template>
            </el-table-column>
          </el-table>
        </el-card>

        <el-card style="margin-top: 20px">
          <template #header>
            <span>通知方式</span>
          </template>
          <el-descriptions :column="1" border>
            <el-descriptions-item label="MQTT通知">
              <el-switch v-model="notificationSettings.mqtt" />
            </el-descriptions-item>
            <el-descriptions-item label="API通知">
              <el-switch v-model="notificationSettings.api" />
            </el-descriptions-item>
            <el-descriptions-item label="邮件通知">
              <el-switch v-model="notificationSettings.email" />
            </el-descriptions-item>
            <el-descriptions-item label="短信通知">
              <el-switch v-model="notificationSettings.sms" />
            </el-descriptions-item>
          </el-descriptions>
          <el-button type="primary" size="small" style="margin-top: 10px; width: 100%" @click="saveNotificationSettings">
            保存通知设置
          </el-button>
        </el-card>
      </el-col>
    </el-row>

    <el-dialog v-model="ruleDialogVisible" :title="isEditRule ? '编辑告警规则' : '新建告警规则'" width="600px">
      <el-form :model="ruleForm" :rules="ruleRules" ref="ruleFormRef" label-width="120px">
        <el-form-item label="规则名称" prop="name">
          <el-input v-model="ruleForm.name" placeholder="请输入规则名称" />
        </el-form-item>
        <el-form-item label="告警级别" prop="level">
          <el-select v-model="ruleForm.level" placeholder="请选择告警级别">
            <el-option label="严重" value="Critical" />
            <el-option label="警告" value="Warning" />
            <el-option label="信息" value="Info" />
          </el-select>
        </el-form-item>
        <el-form-item label="监控对象" prop="source">
          <el-select v-model="ruleForm.source" placeholder="请选择监控对象">
            <el-option label="任务执行" value="task" />
            <el-option label="工作流执行" value="workflow" />
            <el-option label="系统资源" value="system" />
            <el-option label="MQTT消息" value="mqtt" />
            <el-option label="API调用" value="api" />
          </el-select>
        </el-form-item>
        <el-form-item label="触发条件" prop="condition">
          <el-input
            v-model="ruleForm.condition"
            type="textarea"
            :rows="3"
            placeholder="例如: 执行失败次数 > 3"
          />
        </el-form-item>
        <el-form-item label="告警消息" prop="message">
          <el-input
            v-model="ruleForm.message"
            type="textarea"
            :rows="2"
            placeholder="请输入告警消息模板"
          />
        </el-form-item>
        <el-form-item label="启用状态">
          <el-switch v-model="ruleForm.enabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="ruleDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitRuleForm">确定</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="detailDialogVisible" title="告警详情" width="700px">
      <el-descriptions :column="2" border v-if="currentAlert">
        <el-descriptions-item label="告警ID">{{ currentAlert.id }}</el-descriptions-item>
        <el-descriptions-item label="规则名称">{{ currentAlert.ruleName }}</el-descriptions-item>
        <el-descriptions-item label="告警级别">
          <el-tag :type="getLevelTagType(currentAlert.level)" size="small">
            {{ currentAlert.level }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="来源">{{ currentAlert.source }}</el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="getStatusTagType(currentAlert.status)" size="small">
            {{ currentAlert.status }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="触发时间">{{ formatDate(currentAlert.triggerTime) }}</el-descriptions-item>
        <el-descriptions-item label="告警内容" :span="2">{{ currentAlert.message }}</el-descriptions-item>
        <el-descriptions-item label="详细信息" :span="2">
          <pre>{{ currentAlert.details }}</pre>
        </el-descriptions-item>
        <el-descriptions-item label="处理时间" v-if="currentAlert.resolveTime">
          {{ formatDate(currentAlert.resolveTime) }}
        </el-descriptions-item>
        <el-descriptions-item label="处理人" v-if="currentAlert.resolver">
          {{ currentAlert.resolver }}
        </el-descriptions-item>
      </el-descriptions>
      <template #footer>
        <el-button @click="detailDialogVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Plus, Edit, Delete, View, CircleCheck, CircleClose, Warning, Clock } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'

interface Alert {
  id: string
  ruleName: string
  level: string
  source: string
  message: string
  triggerTime: string
  resolveTime: string
  status: string
  resolver: string
  details: string
}

interface AlertRule {
  id: string
  name: string
  level: string
  source: string
  condition: string
  message: string
  enabled: boolean
}

const loading = ref(false)
const activeTab = ref('all')
const alerts = ref<Alert[]>([])
const alertRules = ref<AlertRule[]>([])
const ruleDialogVisible = ref(false)
const detailDialogVisible = ref(false)
const isEditRule = ref(false)
const ruleFormRef = ref()
const currentAlert = ref<Alert | null>(null)

const alertStats = ref({
  total: 0,
  pending: 0,
  resolved: 0,
  ignored: 0
})

const notificationSettings = ref({
  mqtt: true,
  api: true,
  email: false,
  sms: false
})

const ruleForm = ref({
  id: '',
  name: '',
  level: 'Warning',
  source: 'task',
  condition: '',
  message: '',
  enabled: true
})

const ruleRules = {
  name: [{ required: true, message: '请输入规则名称', trigger: 'blur' }],
  level: [{ required: true, message: '请选择告警级别', trigger: 'change' }],
  source: [{ required: true, message: '请选择监控对象', trigger: 'change' }],
  condition: [{ required: true, message: '请输入触发条件', trigger: 'blur' }],
  message: [{ required: true, message: '请输入告警消息', trigger: 'blur' }]
}

const filteredAlerts = computed(() => {
  if (activeTab.value === 'all') {
    return alerts.value
  }
  return alerts.value.filter(alert => alert.status.toLowerCase() === activeTab.value)
})

onMounted(() => {
  loadAlerts()
  loadAlertRules()
})

function loadAlerts() {
  loading.value = true
  setTimeout(() => {
    alerts.value = [
      {
        id: 'alert-1',
        ruleName: '任务执行失败告警',
        level: 'Critical',
        source: 'task',
        message: '任务 task-1 执行失败，错误代码: 500',
        triggerTime: '2024-01-24T10:30:00Z',
        resolveTime: '',
        status: 'Pending',
        resolver: '',
        details: '{"taskId": "task-1", "errorCode": 500, "errorMessage": "Internal Server Error", "retryCount": 3}'
      },
      {
        id: 'alert-2',
        ruleName: '工作流超时告警',
        level: 'Warning',
        source: 'workflow',
        message: '工作流 workflow-1 执行超时，已超过30分钟',
        triggerTime: '2024-01-24T10:15:00Z',
        resolveTime: '2024-01-24T10:20:00Z',
        status: 'Resolved',
        resolver: 'admin',
        details: '{"workflowId": "workflow-1", "timeout": 1800, "actualDuration": 1920}'
      },
      {
        id: 'alert-3',
        ruleName: 'MQTT连接异常',
        level: 'Critical',
        source: 'mqtt',
        message: 'MQTT连接断开，连接失败次数: 5',
        triggerTime: '2024-01-24T09:45:00Z',
        resolveTime: '',
        status: 'Pending',
        resolver: '',
        details: '{"broker": "192.168.91.128:1883", "disconnectCount": 5, "lastError": "Connection timeout"}'
      },
      {
        id: 'alert-4',
        ruleName: '系统资源告警',
        level: 'Warning',
        source: 'system',
        message: 'CPU使用率超过80%',
        triggerTime: '2024-01-24T09:30:00Z',
        resolveTime: '',
        status: 'Ignored',
        resolver: 'admin',
        details: '{"cpuUsage": 85, "memoryUsage": 65, "diskUsage": 45}'
      },
      {
        id: 'alert-5',
        ruleName: 'API调用失败',
        level: 'Critical',
        source: 'api',
        message: '外部API调用失败，状态码: 404',
        triggerTime: '2024-01-24T09:00:00Z',
        resolveTime: '2024-01-24T09:05:00Z',
        status: 'Resolved',
        resolver: 'admin',
        details: '{"url": "https://api.example.com/data", "statusCode": 404, "responseTime": 5000}'
      }
    ]

    alertStats.value = {
      total: alerts.value.length,
      pending: alerts.value.filter(a => a.status === 'Pending').length,
      resolved: alerts.value.filter(a => a.status === 'Resolved').length,
      ignored: alerts.value.filter(a => a.status === 'Ignored').length
    }

    loading.value = false
  }, 500)
}

function loadAlertRules() {
  alertRules.value = [
    {
      id: 'rule-1',
      name: '任务执行失败告警',
      level: 'Critical',
      source: 'task',
      condition: '执行失败次数 > 3',
      message: '任务 ${taskId} 执行失败，错误代码: ${errorCode}',
      enabled: true
    },
    {
      id: 'rule-2',
      name: '工作流超时告警',
      level: 'Warning',
      source: 'workflow',
      condition: '执行时间 > 30分钟',
      message: '工作流 ${workflowId} 执行超时',
      enabled: true
    },
    {
      id: 'rule-3',
      name: 'MQTT连接异常',
      level: 'Critical',
      source: 'mqtt',
      condition: '连接失败次数 > 3',
      message: 'MQTT连接断开',
      enabled: true
    },
    {
      id: 'rule-4',
      name: '系统资源告警',
      level: 'Warning',
      source: 'system',
      condition: 'CPU使用率 > 80%',
      message: '系统资源使用率过高',
      enabled: false
    }
  ]
}

function handleTabClick() {
}

function openCreateRuleDialog() {
  isEditRule.value = false
  ruleForm.value = {
    id: '',
    name: '',
    level: 'Warning',
    source: 'task',
    condition: '',
    message: '',
    enabled: true
  }
  ruleDialogVisible.value = true
}

function editRule(row: AlertRule) {
  isEditRule.value = true
  ruleForm.value = {
    id: row.id,
    name: row.name,
    level: row.level,
    source: row.source,
    condition: row.condition,
    message: row.message,
    enabled: row.enabled
  }
  ruleDialogVisible.value = true
}

function submitRuleForm() {
  ruleFormRef.value.validate((valid: boolean) => {
    if (valid) {
      if (isEditRule.value) {
        const index = alertRules.value.findIndex(r => r.id === ruleForm.value.id)
        if (index !== -1) {
          alertRules.value[index] = { ...ruleForm.value }
        }
        ElMessage.success('告警规则更新成功')
      } else {
        alertRules.value.push({
          ...ruleForm.value,
          id: 'rule-' + Date.now()
        })
        ElMessage.success('告警规则创建成功')
      }
      ruleDialogVisible.value = false
    }
  })
}

function toggleRule(row: AlertRule) {
  ElMessage.success(`告警规则 ${row.name} 已${row.enabled ? '启用' : '禁用'}`)
}

function resolveAlert(row: Alert) {
  ElMessageBox.confirm(`确定要标记告警 "${row.ruleName}" 为已解决吗？`, '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    const index = alerts.value.findIndex(a => a.id === row.id)
    if (index !== -1) {
      alerts.value[index] = {
        ...alerts.value[index],
        status: 'Resolved',
        resolveTime: new Date().toISOString(),
        resolver: 'admin'
      }
      alertStats.value.resolved++
      alertStats.value.pending--
    }
    ElMessage.success('告警已标记为已解决')
  }).catch(() => {})
}

function viewAlertDetails(row: Alert) {
  currentAlert.value = row
  detailDialogVisible.value = true
}

function deleteAlert(row: Alert) {
  ElMessageBox.confirm(`确定要删除告警 "${row.ruleName}" 吗？`, '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    const index = alerts.value.findIndex(a => a.id === row.id)
    if (index !== -1) {
      alerts.value.splice(index, 1)
      alertStats.value.total--
      if (row.status === 'Pending') {
        alertStats.value.pending--
      } else if (row.status === 'Resolved') {
        alertStats.value.resolved--
      } else if (row.status === 'Ignored') {
        alertStats.value.ignored--
      }
    }
    ElMessage.success('告警删除成功')
  }).catch(() => {})
}

function saveNotificationSettings() {
  ElMessage.success('通知设置已保存')
}

function formatDate(dateString: string) {
  const date = new Date(dateString)
  return date.toLocaleString('zh-CN')
}

function getLevelTagType(level: string) {
  const types: Record<string, any> = {
    Critical: 'danger',
    Warning: 'warning',
    Info: 'info'
  }
  return types[level] || ''
}

function getStatusTagType(status: string) {
  const types: Record<string, any> = {
    Pending: 'warning',
    Resolved: 'success',
    Ignored: 'info'
  }
  return types[status] || ''
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

pre {
  background: #f5f5f5;
  padding: 10px;
  border-radius: 4px;
  font-size: 12px;
  max-height: 200px;
  overflow-y: auto;
}
</style>
