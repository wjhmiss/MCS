<template>
  <div class="task-list">
    <el-card>
      <template #header>
        <div class="card-header">
          <span>任务组件库</span>
          <el-button type="primary" @click="showCreateDialog = true">创建任务组件</el-button>
        </div>
      </template>
      
      <el-alert
        title="提示"
        type="info"
        :closable="false"
        style="margin-bottom: 20px"
      >
        任务组件只能作为工作流的组成单元，不能单独执行。请在工作流编辑器中添加任务组件并创建工作流来执行。
      </el-alert>

      <el-table :data="tasks" v-loading="loading" style="width: 100%">
        <el-table-column prop="name" label="任务名称" width="200" />
        <el-table-column prop="taskType" label="任务类型" width="120">
          <template #default="{ row }">
            <el-tag :type="getTaskTypeTagType(row.taskType)">
              {{ getTaskTypeLabel(row.taskType) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="description" label="描述" show-overflow-tooltip />
        <el-table-column prop="isEnabled" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isEnabled ? 'success' : 'info'">
              {{ row.isEnabled ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="200" fixed="right">
          <template #default="{ row }">
            <el-button size="small" @click="editTask(row)">编辑</el-button>
            <el-button size="small" type="danger" @click="deleteTask(row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="showCreateDialog" :title="isEdit ? '编辑任务组件' : '创建任务组件'" width="600px">
      <el-form :model="taskForm" label-width="100px">
        <el-form-item label="任务名称">
          <el-input v-model="taskForm.name" placeholder="请输入任务名称" />
        </el-form-item>
        <el-form-item label="任务类型">
          <el-select v-model="taskForm.taskType" placeholder="请选择任务类型">
            <el-option label="API调用" value="api" />
            <el-option label="MQTT" value="mqtt" />
            <el-option label="延迟" value="delay" />
            <el-option label="条件判断" value="condition" />
            <el-option label="脚本" value="script" />
            <el-option label="数据处理" value="data" />
            <el-option label="错误处理" value="error" />
          </el-select>
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="taskForm.description" type="textarea" :rows="3" placeholder="请输入描述" />
        </el-form-item>
        <el-form-item label="配置参数">
          <el-input
            v-model="configJson"
            type="textarea"
            :rows="8"
            placeholder='{"url": "https://api.example.com", "method": "GET"}'
          />
        </el-form-item>
        <el-form-item label="启用状态">
          <el-switch v-model="taskForm.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCreateDialog = false">取消</el-button>
        <el-button type="primary" @click="saveTask">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useTaskStore } from '@/stores/tasks'
import { ElMessage, ElMessageBox } from 'element-plus'
import dayjs from 'dayjs'

const taskStore = useTaskStore()
const tasks = ref<any[]>([])
const loading = ref(false)
const showCreateDialog = ref(false)
const isEdit = ref(false)
const configJson = ref('')

const taskForm = ref({
  id: '',
  name: '',
  taskType: 'api',
  description: '',
  config: {},
  isEnabled: true
})

onMounted(() => {
  loadTasks()
})

async function loadTasks() {
  loading.value = true
  try {
    tasks.value = [
      {
        id: 'task-1',
        name: 'API测试任务',
        taskType: 'api',
        description: '测试外部API调用',
        isEnabled: true,
        createdAt: new Date().toISOString(),
        config: {
          url: 'https://api.example.com/test',
          method: 'GET',
          timeout: 30000
        }
      },
      {
        id: 'task-2',
        name: 'MQTT发布任务',
        taskType: 'mqtt',
        description: '发布MQTT消息',
        isEnabled: true,
        createdAt: new Date().toISOString(),
        config: {
          topic: 'test/topic',
          qos: 0
        }
      },
      {
        id: 'task-3',
        name: '延迟任务',
        taskType: 'delay',
        description: '延迟5秒',
        isEnabled: true,
        createdAt: new Date().toISOString(),
        config: {
          delayMs: 5000
        }
      },
      {
        id: 'task-4',
        name: '条件判断任务',
        taskType: 'condition',
        description: '根据条件判断执行',
        isEnabled: true,
        createdAt: new Date().toISOString(),
        config: {
          condition: 'result.success === true'
        }
      },
      {
        id: 'task-5',
        name: '脚本任务',
        taskType: 'script',
        description: '执行自定义脚本',
        isEnabled: true,
        createdAt: new Date().toISOString(),
        config: {
          script: 'console.log("Hello World")'
        }
      },
      {
        id: 'task-6',
        name: '数据处理任务',
        taskType: 'data',
        description: '处理和转换数据',
        isEnabled: true,
        createdAt: new Date().toISOString(),
        config: {
          transform: 'json'
        }
      },
      {
        id: 'task-7',
        name: '错误处理任务',
        taskType: 'error',
        description: '处理执行错误',
        isEnabled: true,
        createdAt: new Date().toISOString(),
        config: {
          retryCount: 3,
          onError: 'log'
        }
      }
    ]
  } catch (error: any) {
    ElMessage.error('加载任务失败: ' + error.message)
  } finally {
    loading.value = false
  }
}

function editTask(row: any) {
  isEdit.value = true
  taskForm.value = {
    id: row.id,
    name: row.name,
    taskType: row.taskType,
    description: row.description,
    config: row.config,
    isEnabled: row.isEnabled
  }
  configJson.value = JSON.stringify(row.config, null, 2)
  showCreateDialog.value = true
}

function deleteTask(taskId: string) {
  ElMessageBox.confirm('确定要删除这个任务组件吗？', '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    const index = tasks.value.findIndex(t => t.id === taskId)
    if (index !== -1) {
      tasks.value.splice(index, 1)
      ElMessage.success('任务组件已删除')
    }
  }).catch(() => {})
}

function saveTask() {
  try {
    taskForm.value.config = JSON.parse(configJson.value)
    
    if (isEdit.value) {
      const index = tasks.value.findIndex(t => t.id === taskForm.value.id)
      if (index !== -1) {
        tasks.value[index] = {
          ...taskForm.value,
          createdAt: tasks.value[index].createdAt
        }
        ElMessage.success('任务组件更新成功')
      }
    } else {
      tasks.value.push({
        id: 'task-' + Date.now(),
        ...taskForm.value,
        createdAt: new Date().toISOString()
      })
      ElMessage.success('任务组件创建成功')
    }
    
    showCreateDialog.value = false
  } catch (error) {
    ElMessage.error('配置参数JSON格式错误')
  }
}

function getTaskTypeLabel(type: string) {
  const labels: Record<string, string> = {
    api: 'API调用',
    mqtt: 'MQTT',
    delay: '延迟',
    condition: '条件判断',
    script: '脚本',
    data: '数据处理',
    error: '错误处理'
  }
  return labels[type] || type
}

function getTaskTypeTagType(type: string) {
  const types: Record<string, any> = {
    api: 'primary',
    mqtt: 'success',
    delay: 'warning',
    condition: 'info',
    script: 'danger',
    data: '',
    error: 'danger'
  }
  return types[type] || ''
}

function formatDate(date: string) {
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}
</script>

<style scoped>
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
