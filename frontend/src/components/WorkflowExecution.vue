<template>
  <div class="workflow-execution">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>工作流执行历史</span>
          <div class="header-actions">
            <el-button @click="loadExecutions">刷新</el-button>
          </div>
        </div>
      </template>

      <el-alert
        title="提示"
        type="info"
        :closable="false"
        style="margin-bottom: 20px"
      >
        此页面显示工作流的执行历史记录。任务组件只能作为工作流的组成单元，不能单独执行。
      </el-alert>

      <el-table :data="executions" v-loading="loading" style="width: 100%">
        <el-table-column prop="id" label="执行ID" width="100" />
        <el-table-column prop="workflowName" label="工作流名称" width="180" />
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusTagType(row.status)">
              {{ getStatusLabel(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="startTime" label="开始时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.startTime) }}
          </template>
        </el-table-column>
        <el-table-column prop="endTime" label="结束时间" width="180">
          <template #default="{ row }">
            {{ row.endTime ? formatDate(row.endTime) : '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="duration" label="耗时" width="120">
          <template #default="{ row }">
            {{ calculateDuration(row.startTime, row.endTime) }}
          </template>
        </el-table-column>
        <el-table-column prop="triggeredBy" label="触发者" width="120" />
        <el-table-column prop="triggerType" label="触发类型" width="100">
          <template #default="{ row }">
            <el-tag size="small" :type="getTriggerTypeTagType(row.triggerType)">
              {{ getTriggerTypeLabel(row.triggerType) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button size="small" @click="viewDetails(row)">详情</el-button>
            <el-button size="small" type="danger" @click="terminateExecution(row.id)" v-if="row.status === 'Running'">终止</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="detailsVisible" title="工作流执行详情" width="900px">
      <el-descriptions v-if="selectedExecution" :column="2" border>
        <el-descriptions-item label="执行ID">{{ selectedExecution.id }}</el-descriptions-item>
        <el-descriptions-item label="工作流名称">{{ selectedExecution.workflowName }}</el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="getStatusTagType(selectedExecution.status)">
            {{ getStatusLabel(selectedExecution.status) }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="耗时">{{ calculateDuration(selectedExecution.startTime, selectedExecution.endTime) }}</el-descriptions-item>
        <el-descriptions-item label="开始时间">{{ formatDate(selectedExecution.startTime) }}</el-descriptions-item>
        <el-descriptions-item label="结束时间">{{ selectedExecution.endTime ? formatDate(selectedExecution.endTime) : '-' }}</el-descriptions-item>
        <el-descriptions-item label="触发者">{{ selectedExecution.triggeredBy || '-' }}</el-descriptions-item>
        <el-descriptions-item label="触发类型">
          <el-tag size="small" :type="getTriggerTypeTagType(selectedExecution.triggerType)">
            {{ getTriggerTypeLabel(selectedExecution.triggerType) }}
          </el-tag>
        </el-descriptions-item>
      </el-descriptions>

      <el-divider>输入数据</el-divider>
      <pre class="json-display">{{ formatJson(selectedExecution?.inputData) }}</pre>

      <el-divider>输出数据</el-divider>
      <pre class="json-display">{{ formatJson(selectedExecution?.outputData) }}</pre>

      <el-divider>任务组件执行状态</el-divider>
      <el-table :data="nodeStatuses" style="width: 100%">
        <el-table-column prop="taskName" label="任务名称" width="150" />
        <el-table-column prop="taskType" label="任务类型" width="120">
          <template #default="{ row }">
            <el-tag size="small" :type="getTaskTypeTagType(row.taskType)">
              {{ getTaskTypeLabel(row.taskType) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusTagType(row.status)">
              {{ getStatusLabel(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="startTime" label="开始时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.startTime) }}
          </template>
        </el-table-column>
        <el-table-column prop="endTime" label="结束时间" width="180">
          <template #default="{ row }">
            {{ row.endTime ? formatDate(row.endTime) : '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" show-overflow-tooltip />
      </el-table>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useWorkflowStore } from '@/stores/workflows'
import { ElMessage } from 'element-plus'
import dayjs from 'dayjs'
import signalrService from '@/services/signalr'

const workflowStore = useWorkflowStore()
const executions = ref<any[]>([])
const loading = ref(false)
const detailsVisible = ref(false)
const selectedExecution = ref<any>(null)
const nodeStatuses = ref<any[]>([])

onMounted(() => {
  loadExecutions()
  setupSignalR()
})

onUnmounted(() => {
  signalrService.offWorkflowUpdated(handleWorkflowUpdate)
})

async function loadExecutions() {
  loading.value = true
  try {
    executions.value = [
      {
        id: 'exec-1',
        workflowName: '数据处理工作流',
        status: 'Completed',
        startTime: new Date(Date.now() - 3600000).toISOString(),
        endTime: new Date(Date.now() - 3500000).toISOString(),
        triggeredBy: 'admin',
        triggerType: 'Manual',
        inputData: { test: 'data' },
        outputData: { result: 'success' },
        nodeStatuses: [
          {
            taskName: 'API测试任务',
            taskType: 'api',
            status: 'Completed',
            startTime: new Date(Date.now() - 3600000).toISOString(),
            endTime: new Date(Date.now() - 3580000).toISOString(),
            errorMessage: ''
          },
          {
            taskName: '数据处理任务',
            taskType: 'data',
            status: 'Completed',
            startTime: new Date(Date.now() - 3580000).toISOString(),
            endTime: new Date(Date.now() - 3500000).toISOString(),
            errorMessage: ''
          }
        ]
      },
      {
        id: 'exec-2',
        workflowName: '备份工作流',
        status: 'Running',
        startTime: new Date().toISOString(),
        endTime: null,
        triggeredBy: 'system',
        triggerType: 'Scheduled',
        inputData: { test: 'data2' },
        outputData: null,
        nodeStatuses: [
          {
            taskName: '延迟任务',
            taskType: 'delay',
            status: 'Running',
            startTime: new Date().toISOString(),
            endTime: null,
            errorMessage: ''
          }
        ]
      },
      {
        id: 'exec-3',
        workflowName: '数据处理工作流',
        status: 'Failed',
        startTime: new Date(Date.now() - 7200000).toISOString(),
        endTime: new Date(Date.now() - 7100000).toISOString(),
        triggeredBy: 'admin',
        triggerType: 'Manual',
        inputData: { test: 'data3' },
        outputData: null,
        nodeStatuses: [
          {
            taskName: 'API测试任务',
            taskType: 'api',
            status: 'Failed',
            startTime: new Date(Date.now() - 7200000).toISOString(),
            endTime: new Date(Date.now() - 7100000).toISOString(),
            errorMessage: 'API调用超时'
          }
        ]
      }
    ]
  } catch (error: any) {
    ElMessage.error('加载执行历史失败: ' + error.message)
  } finally {
    loading.value = false
  }
}

function setupSignalR() {
  signalrService.onWorkflowUpdated(handleWorkflowUpdate)
  signalrService.connect()
}

function handleWorkflowUpdate(workflowId: string, status: string) {
  const execution = executions.value.find(e => e.workflowDefinitionId === workflowId && e.status === 'Running')
  if (execution) {
    execution.status = status
    if (status !== 'Running') {
      execution.endTime = new Date().toISOString()
    }
  }
}

function viewDetails(execution: any) {
  selectedExecution.value = execution
  nodeStatuses.value = execution.nodeStatuses || []
  detailsVisible.value = true
}

async function terminateExecution(executionId: string) {
  try {
    await workflowStore.terminateWorkflow(executionId)
    ElMessage.success('执行已终止')
    loadExecutions()
  } catch (error: any) {
    ElMessage.error('终止执行失败: ' + error.message)
  }
}

function getStatusTagType(status: string) {
  const types: Record<string, any> = {
    Pending: 'info',
    Running: 'primary',
    Completed: 'success',
    Failed: 'danger',
    Stopped: 'warning',
    Terminated: 'danger'
  }
  return types[status] || ''
}

function getStatusLabel(status: string) {
  const labels: Record<string, string> = {
    Pending: '待执行',
    Running: '运行中',
    Completed: '已完成',
    Failed: '失败',
    Stopped: '已停止',
    Terminated: '已终止'
  }
  return labels[status] || status
}

function getTriggerTypeTagType(triggerType: string) {
  const types: Record<string, any> = {
    Manual: 'primary',
    Scheduled: 'success',
    API: 'warning',
    MQTT: 'info'
  }
  return types[triggerType] || ''
}

function getTriggerTypeLabel(triggerType: string) {
  const labels: Record<string, string> = {
    Manual: '手动触发',
    Scheduled: '定时触发',
    API: 'API触发',
    MQTT: 'MQTT触发'
  }
  return labels[triggerType] || triggerType
}

function getTaskTypeTagType(taskType: string) {
  const types: Record<string, any> = {
    api: 'primary',
    mqtt: 'success',
    delay: 'warning',
    condition: 'info',
    script: 'danger',
    data: '',
    error: 'danger'
  }
  return types[taskType] || ''
}

function getTaskTypeLabel(taskType: string) {
  const labels: Record<string, string> = {
    api: 'API调用',
    mqtt: 'MQTT',
    delay: '延迟',
    condition: '条件判断',
    script: '脚本',
    data: '数据处理',
    error: '错误处理'
  }
  return labels[taskType] || taskType
}

function calculateDuration(startTime: string, endTime: string | null) {
  if (!endTime) return '-'
  const start = new Date(startTime).getTime()
  const end = new Date(endTime).getTime()
  const duration = end - start
  
  if (duration < 1000) return `${duration}ms`
  if (duration < 60000) return `${(duration / 1000).toFixed(2)}s`
  if (duration < 3600000) return `${(duration / 60000).toFixed(2)}m`
  return `${(duration / 3600000).toFixed(2)}h`
}

function formatDate(date: string) {
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}

function formatJson(data: any) {
  return data ? JSON.stringify(data, null, 2) : '{}'
}
</script>

<style scoped>
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.json-display {
  background: #f5f5f5;
  padding: 10px;
  border-radius: 4px;
  overflow-x: auto;
}
</style>
