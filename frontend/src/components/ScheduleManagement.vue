<template>
  <div class="schedule-management">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>定时任务管理</span>
          <el-button type="primary" :icon="Plus" @click="openCreateDialog">新建定时任务</el-button>
        </div>
      </template>

      <el-table :data="schedules" style="width: 100%" v-loading="loading">
        <el-table-column prop="id" label="ID" width="80" />
        <el-table-column prop="name" label="任务名称" width="150" />
        <el-table-column label="任务类型" width="100">
          <template #default="{ row }">
            <el-tag :type="row.taskDefinitionId ? 'primary' : 'success'" size="small">
              {{ row.taskDefinitionId ? '任务' : '工作流' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="cronExpression" label="Cron表达式" width="150" />
        <el-table-column label="下次执行时间" width="180">
          <template #default="{ row }">
            {{ row.nextRunTime ? formatDate(row.nextRunTime) : '-' }}
          </template>
        </el-table-column>
        <el-table-column label="上次执行时间" width="180">
          <template #default="{ row }">
            {{ row.lastRunTime ? formatDate(row.lastRunTime) : '-' }}
          </template>
        </el-table-column>
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-switch
              v-model="row.isEnabled"
              @change="toggleSchedule(row)"
              active-text="启用"
              inactive-text="禁用"
            />
          </template>
        </el-table-column>
        <el-table-column label="操作" width="250" fixed="right">
          <template #default="{ row }">
            <el-button size="small" :icon="VideoPlay" @click="triggerSchedule(row)">立即执行</el-button>
            <el-button size="small" :icon="Edit" @click="editSchedule(row)">编辑</el-button>
            <el-button size="small" type="danger" :icon="Delete" @click="deleteSchedule(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog
      v-model="dialogVisible"
      :title="isEdit ? '编辑定时任务' : '新建定时任务'"
      width="600px"
    >
      <el-form :model="form" :rules="rules" ref="formRef" label-width="120px">
        <el-form-item label="任务名称" prop="name">
          <el-input v-model="form.name" placeholder="请输入任务名称" />
        </el-form-item>
        <el-form-item label="任务类型" prop="taskType">
          <el-radio-group v-model="form.taskType">
            <el-radio label="task">任务</el-radio>
            <el-radio label="workflow">工作流</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="选择任务" prop="taskDefinitionId" v-if="form.taskType === 'task'">
          <el-select v-model="form.taskDefinitionId" placeholder="请选择任务" style="width: 100%">
            <el-option
              v-for="task in tasks"
              :key="task.id"
              :label="task.name"
              :value="task.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="选择工作流" prop="workflowDefinitionId" v-if="form.taskType === 'workflow'">
          <el-select v-model="form.workflowDefinitionId" placeholder="请选择工作流" style="width: 100%">
            <el-option
              v-for="workflow in workflows"
              :key="workflow.id"
              :label="workflow.name"
              :value="workflow.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="Cron表达式" prop="cronExpression">
          <el-input v-model="form.cronExpression" placeholder="例如: 0 0 12 * * ?" />
          <div class="cron-help">
            <el-link type="primary" @click="showCronHelp = true">查看Cron表达式帮助</el-link>
          </div>
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input
            v-model="form.description"
            type="textarea"
            :rows="3"
            placeholder="请输入任务描述"
          />
        </el-form-item>
        <el-form-item label="启用状态">
          <el-switch v-model="form.isEnabled" active-text="启用" inactive-text="禁用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitForm">确定</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showCronHelp" title="Cron表达式帮助" width="800px">
      <el-table :data="cronExamples" style="width: 100%">
        <el-table-column prop="expression" label="Cron表达式" width="200" />
        <el-table-column prop="description" label="说明" />
        <el-table-column prop="example" label="示例" />
      </el-table>
      <template #footer>
        <el-button @click="showCronHelp = false">关闭</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="historyVisible" title="执行历史" width="800px">
      <el-table :data="executionHistory" style="width: 100%">
        <el-table-column prop="id" label="ID" width="80" />
        <el-table-column prop="scheduleId" label="定时任务ID" width="120" />
        <el-table-column label="执行时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.executionTime) }}
          </template>
        </el-table-column>
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="getStatusTagType(row.status)" size="small">
              {{ row.status }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="message" label="消息" />
      </el-table>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Plus, Edit, Delete, VideoPlay } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'

interface Schedule {
  id: string
  name: string
  taskDefinitionId: string
  workflowDefinitionId: string
  cronExpression: string
  isEnabled: boolean
  lastRunTime: string
  nextRunTime: string
  description: string
}

interface Task {
  id: string
  name: string
}

interface Workflow {
  id: string
  name: string
}

const loading = ref(false)
const schedules = ref<Schedule[]>([])
const tasks = ref<Task[]>([])
const workflows = ref<Workflow[]>([])
const dialogVisible = ref(false)
const historyVisible = ref(false)
const showCronHelp = ref(false)
const isEdit = ref(false)
const formRef = ref()

const form = ref({
  id: '',
  name: '',
  taskType: 'task',
  taskDefinitionId: '',
  workflowDefinitionId: '',
  cronExpression: '',
  description: '',
  isEnabled: true
})

const rules = {
  name: [{ required: true, message: '请输入任务名称', trigger: 'blur' }],
  taskType: [{ required: true, message: '请选择任务类型', trigger: 'change' }],
  taskDefinitionId: [{ required: true, message: '请选择任务', trigger: 'change' }],
  workflowDefinitionId: [{ required: true, message: '请选择工作流', trigger: 'change' }],
  cronExpression: [{ required: true, message: '请输入Cron表达式', trigger: 'blur' }]
}

const cronExamples = ref([
  { expression: '0 0 12 * * ?', description: '每天中午12点', example: '12:00:00' },
  { expression: '0 0 10 * * ?', description: '每天上午10点', example: '10:00:00' },
  { expression: '0 0/5 * * * ?', description: '每5分钟', example: '00:00, 00:05, 00:10...' },
  { expression: '0 0 0 * * ?', description: '每天午夜', example: '00:00:00' },
  { expression: '0 0 12 ? * MON-FRI', description: '工作日中午12点', example: '周一至周五 12:00:00' },
  { expression: '0 0 0 1 * ?', description: '每月1号午夜', example: '每月1号 00:00:00' },
  { expression: '0 0 0 ? * SUN', description: '每周日午夜', example: '周日 00:00:00' },
  { expression: '0 0 0 1 1 ?', description: '每年1月1号午夜', example: '1月1号 00:00:00' }
])

const executionHistory = ref([
  { id: '1', scheduleId: 'schedule-1', executionTime: '2024-01-24T10:00:00Z', status: 'Success', message: '执行成功' },
  { id: '2', scheduleId: 'schedule-1', executionTime: '2024-01-24T09:00:00Z', status: 'Success', message: '执行成功' },
  { id: '3', scheduleId: 'schedule-2', executionTime: '2024-01-24T08:30:00Z', status: 'Failed', message: '任务执行失败' },
  { id: '4', scheduleId: 'schedule-1', executionTime: '2024-01-24T08:00:00Z', status: 'Success', message: '执行成功' }
])

onMounted(() => {
  loadSchedules()
  loadTasks()
  loadWorkflows()
})

function loadSchedules() {
  loading.value = true
  setTimeout(() => {
    schedules.value = [
      {
        id: 'schedule-1',
        name: '每日数据同步',
        taskDefinitionId: 'task-1',
        workflowDefinitionId: '',
        cronExpression: '0 0 2 * * ?',
        isEnabled: true,
        lastRunTime: '2024-01-24T02:00:00Z',
        nextRunTime: '2024-01-25T02:00:00Z',
        description: '每天凌晨2点执行数据同步任务'
      },
      {
        id: 'schedule-2',
        name: '每小时备份',
        taskDefinitionId: '',
        workflowDefinitionId: 'workflow-1',
        cronExpression: '0 0 * * * ?',
        isEnabled: true,
        lastRunTime: '2024-01-24T10:00:00Z',
        nextRunTime: '2024-01-24T11:00:00Z',
        description: '每小时执行一次备份工作流'
      },
      {
        id: 'schedule-3',
        name: '每周报告',
        taskDefinitionId: 'task-2',
        workflowDefinitionId: '',
        cronExpression: '0 0 9 ? * MON',
        isEnabled: false,
        lastRunTime: '2024-01-22T09:00:00Z',
        nextRunTime: '2024-01-29T09:00:00Z',
        description: '每周一上午9点生成报告'
      }
    ]
    loading.value = false
  }, 500)
}

function loadTasks() {
  tasks.value = [
    { id: 'task-1', name: '数据同步任务' },
    { id: 'task-2', name: '报告生成任务' },
    { id: 'task-3', name: 'API调用任务' },
    { id: 'task-4', name: 'MQTT发布任务' }
  ]
}

function loadWorkflows() {
  workflows.value = [
    { id: 'workflow-1', name: '备份工作流' },
    { id: 'workflow-2', name: '数据处理工作流' },
    { id: 'workflow-3', name: '监控告警工作流' }
  ]
}

function openCreateDialog() {
  isEdit.value = false
  form.value = {
    id: '',
    name: '',
    taskType: 'task',
    taskDefinitionId: '',
    workflowDefinitionId: '',
    cronExpression: '',
    description: '',
    isEnabled: true
  }
  dialogVisible.value = true
}

function editSchedule(row: Schedule) {
  isEdit.value = true
  form.value = {
    id: row.id,
    name: row.name,
    taskType: row.taskDefinitionId ? 'task' : 'workflow',
    taskDefinitionId: row.taskDefinitionId,
    workflowDefinitionId: row.workflowDefinitionId,
    cronExpression: row.cronExpression,
    description: row.description,
    isEnabled: row.isEnabled
  }
  dialogVisible.value = true
}

function submitForm() {
  formRef.value.validate((valid: boolean) => {
    if (valid) {
      if (isEdit.value) {
        const index = schedules.value.findIndex(s => s.id === form.value.id)
        if (index !== -1) {
          schedules.value[index] = {
            ...schedules.value[index],
            name: form.value.name,
            taskDefinitionId: form.value.taskDefinitionId,
            workflowDefinitionId: form.value.workflowDefinitionId,
            cronExpression: form.value.cronExpression,
            description: form.value.description,
            isEnabled: form.value.isEnabled
          }
        }
        ElMessage.success('定时任务更新成功')
      } else {
        const newSchedule: Schedule = {
          id: 'schedule-' + Date.now(),
          name: form.value.name,
          taskDefinitionId: form.value.taskDefinitionId,
          workflowDefinitionId: form.value.workflowDefinitionId,
          cronExpression: form.value.cronExpression,
          isEnabled: form.value.isEnabled,
          lastRunTime: '',
          nextRunTime: '',
          description: form.value.description
        }
        schedules.value.push(newSchedule)
        ElMessage.success('定时任务创建成功')
      }
      dialogVisible.value = false
    }
  })
}

function toggleSchedule(row: Schedule) {
  ElMessage.success(`定时任务 ${row.name} 已${row.isEnabled ? '启用' : '禁用'}`)
}

function triggerSchedule(row: Schedule) {
  ElMessageBox.confirm(`确定要立即执行定时任务 "${row.name}" 吗？`, '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    ElMessage.success('定时任务执行成功')
    historyVisible.value = true
  }).catch(() => {})
}

function deleteSchedule(row: Schedule) {
  ElMessageBox.confirm(`确定要删除定时任务 "${row.name}" 吗？`, '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    const index = schedules.value.findIndex(s => s.id === row.id)
    if (index !== -1) {
      schedules.value.splice(index, 1)
      ElMessage.success('定时任务删除成功')
    }
  }).catch(() => {})
}

function formatDate(dateString: string) {
  if (!dateString) return '-'
  const date = new Date(dateString)
  return date.toLocaleString('zh-CN')
}

function getStatusTagType(status: string) {
  const types: Record<string, any> = {
    Success: 'success',
    Failed: 'danger',
    Running: 'primary',
    Pending: 'info'
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

.cron-help {
  margin-top: 5px;
  font-size: 12px;
}
</style>
