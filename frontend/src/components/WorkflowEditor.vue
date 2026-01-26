<template>
  <div class="workflow-editor">
    <el-container>
      <el-header>
        <div class="header-content">
          <h2>工作流编辑器</h2>
          <div class="header-actions">
            <el-button @click="showWorkflowList">查看工作流列表</el-button>
            <el-button type="primary" @click="saveWorkflow">保存工作流</el-button>
            <el-button @click="clearWorkflow">清空画布</el-button>
            <el-button type="success" @click="startWorkflow">启动工作流</el-button>
            <el-button type="warning" @click="stopWorkflow">停止工作流</el-button>
          </div>
        </div>
      </el-header>
      <el-container>
        <el-aside width="280px">
          <div class="task-palette">
            <h3>任务组件库</h3>
            <el-alert
              title="提示"
              type="info"
              :closable="false"
              style="margin-bottom: 15px; font-size: 12px"
            >
              从任务组件库中拖拽任务到画布，或点击"添加"按钮直接添加
            </el-alert>
            <div
              v-for="task in availableTasks"
              :key="task.id"
              class="task-item"
              draggable="true"
              @dragstart="onDragStart($event, task)"
            >
              <div class="task-icon">
                <el-icon><component :is="getTaskIcon(task.taskType)" /></el-icon>
              </div>
              <div class="task-info">
                <div class="task-name">{{ task.name }}</div>
                <div class="task-type">{{ getTaskTypeLabel(task.taskType) }}</div>
              </div>
              <el-button size="small" @click="addTaskToCanvas(task)">添加</el-button>
            </div>
          </div>
        </el-aside>
        <el-main>
          <div id="graph-container" class="graph-container" @drop="onDrop" @dragover.prevent></div>
        </el-main>
      </el-container>
    </el-container>

    <el-drawer v-model="nodeConfigVisible" title="节点配置" size="450px">
      <el-form v-if="selectedNode" :model="nodeConfig" label-width="100px">
        <el-form-item label="节点名称">
          <el-input v-model="nodeConfig.name" />
        </el-form-item>
        <el-form-item label="任务类型">
          <el-input v-model="nodeConfig.taskType" disabled />
        </el-form-item>
        <el-form-item label="执行顺序">
          <el-input-number v-model="nodeConfig.executionOrder" :min="1" />
        </el-form-item>
        <el-form-item label="并发执行">
          <el-switch v-model="nodeConfig.isConcurrent" />
        </el-form-item>
        <el-form-item label="等待前置">
          <el-switch v-model="nodeConfig.waitForPrevious" />
        </el-form-item>
        <el-form-item label="跳过条件">
          <el-input v-model="skipConditionInput" placeholder="输入条件后回车添加" @keyup.enter="addSkipCondition" />
          <div class="tags-container">
            <el-tag
              v-for="(condition, index) in nodeConfig.skipConditions"
              :key="index"
              closable
              @close="removeSkipCondition(index)"
            >
              {{ condition }}
            </el-tag>
          </div>
        </el-form-item>
        <el-form-item label="配置参数">
          <el-input
            v-model="configJson"
            type="textarea"
            :rows="8"
            placeholder='{"key": "value"}'
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="applyNodeConfig">应用配置</el-button>
          <el-button @click="deleteSelectedNode">从工作流中移除</el-button>
        </el-form-item>
      </el-form>
    </el-drawer>

    <el-dialog v-model="workflowListVisible" title="工作流列表" width="800px">
      <el-table :data="savedWorkflows" style="width: 100%">
        <el-table-column prop="name" label="工作流名称" width="200" />
        <el-table-column prop="description" label="描述" show-overflow-tooltip />
        <el-table-column prop="nodeCount" label="节点数" width="100" />
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="250" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="primary" @click="loadWorkflow(row)">加载</el-button>
            <el-button size="small" @click="editWorkflow(row)">编辑</el-button>
            <el-button size="small" type="success" @click="executeWorkflow(row)">执行</el-button>
            <el-button size="small" type="danger" @click="deleteWorkflow(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Graph } from '@antv/x6'
import { useWorkflowStore } from '@/stores/workflows'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  Connection,
  Setting,
  Timer,
  Message,
  Document,
  DataLine,
  Warning
} from '@element-plus/icons-vue'
import dayjs from 'dayjs'

const workflowStore = useWorkflowStore()
const graph = ref<Graph | null>(null)
const nodeConfigVisible = ref(false)
const workflowListVisible = ref(false)
const selectedNode = ref<any>(null)
const skipConditionInput = ref('')
const configJson = ref('')

const availableTasks = ref([
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
])

const savedWorkflows = ref([
  {
    id: 'workflow-1',
    name: '数据处理工作流',
    description: '数据处理和转换流程',
    nodeCount: 5,
    createdAt: new Date().toISOString()
  },
  {
    id: 'workflow-2',
    name: '备份工作流',
    description: '系统备份流程',
    nodeCount: 3,
    createdAt: new Date().toISOString()
  }
])

const nodeConfig = ref({
  id: '',
  name: '',
  taskType: '',
  executionOrder: 1,
  isConcurrent: false,
  waitForPrevious: true,
  skipConditions: [] as string[],
  config: {} as Record<string, any>
})

onMounted(() => {
  initGraph()
})

onUnmounted(() => {
  graph.value?.dispose()
})

function initGraph() {
  graph.value = new Graph({
    container: document.getElementById('graph-container')!,
    width: '100%',
    height: '100%',
    background: {
      color: '#f5f5f5'
    },
    grid: {
      size: 10,
      visible: true,
      type: 'dot',
      args: {
        color: '#d0d0d0',
        thickness: 1
      }
    },
    panning: {
      enabled: true,
      modifiers: 'shift'
    },
    mousewheel: {
      enabled: true,
      modifiers: 'ctrl',
      factor: 1.1,
      maxScale: 1.5,
      minScale: 0.5
    },
    highlighting: {
      magnetAdsorbed: {
        name: 'stroke',
        args: {
          attrs: {
            fill: '#5F95FF',
            stroke: '#5F95FF'
          }
        }
      }
    },
    connecting: {
      snap: true,
      allowBlank: false,
      allowLoop: false,
      allowNode: false,
      allowEdge: false,
      allowMulti: false,
      router: {
        name: 'manhattan'
      },
      connector: {
        name: 'rounded',
        args: {
          radius: 8
        }
      },
      createEdge() {
        return graph.value!.createEdge({
          attrs: {
            line: {
              stroke: '#A2B1C3',
              strokeWidth: 2,
              targetMarker: {
                name: 'classic',
                size: 10
              }
            }
          },
          zIndex: 0
        })
      },
      validateConnection({ targetMagnet }) {
        return !!targetMagnet
      }
    }
  })

  graph.value.on('node:click', ({ node }) => {
    selectedNode.value = node
    const data = node.getData()
    nodeConfig.value = {
      id: node.id,
      name: data.name || '',
      taskType: data.taskType || '',
      executionOrder: data.executionOrder || 1,
      isConcurrent: data.isConcurrent || false,
      waitForPrevious: data.waitForPrevious !== false,
      skipConditions: data.skipConditions || [],
      config: data.config || {}
    }
    configJson.value = JSON.stringify(nodeConfig.value.config, null, 2)
    nodeConfigVisible.value = true
  })

  graph.value.on('edge:click', ({ edge }) => {
    edge.remove()
  })

  graph.value.on('blank:click', () => {
    selectedNode.value = null
    nodeConfigVisible.value = false
  })
}

function onDragStart(event: DragEvent, task: any) {
  event.dataTransfer?.setData('task', JSON.stringify(task))
}

function onDrop(event: DragEvent) {
  const taskStr = event.dataTransfer?.getData('task')
  if (!taskStr || !graph.value) return

  const task = JSON.parse(taskStr)
  const container = document.getElementById('graph-container')
  if (!container) return

  const rect = container.getBoundingClientRect()
  const x = event.clientX - rect.left
  const y = event.clientY - rect.top

  addTaskToCanvas(task, x, y)
}

function addTaskToCanvas(task: any, x?: number, y?: number) {
  if (!graph.value) return

  const container = document.getElementById('graph-container')
  if (!container) return

  const rect = container.getBoundingClientRect()
  const posX = x !== undefined ? x : rect.width / 2 - 60
  const posY = y !== undefined ? y : rect.height / 2 - 30

  const node = graph.value.addNode({
    x: posX,
    y: posY,
    width: 120,
    height: 60,
    shape: 'rect',
    ports: {
      groups: {
        top: {
          position: 'top',
          attrs: {
            circle: {
              r: 6,
              magnet: true,
              stroke: '#5F95FF',
              strokeWidth: 2,
              fill: '#fff'
            }
          }
        },
        right: {
          position: 'right',
          attrs: {
            circle: {
              r: 6,
              magnet: true,
              stroke: '#5F95FF',
              strokeWidth: 2,
              fill: '#fff'
            }
          }
        },
        bottom: {
          position: 'bottom',
          attrs: {
            circle: {
              r: 6,
              magnet: true,
              stroke: '#5F95FF',
              strokeWidth: 2,
              fill: '#fff'
            }
          }
        },
        left: {
          position: 'left',
          attrs: {
            circle: {
              r: 6,
              magnet: true,
              stroke: '#5F95FF',
              strokeWidth: 2,
              fill: '#fff'
            }
          }
        }
      },
      items: [
        { id: 'top', group: 'top' },
        { id: 'right', group: 'right' },
        { id: 'bottom', group: 'bottom' },
        { id: 'left', group: 'left' }
      ]
    },
    attrs: {
      body: {
        fill: '#ffffff',
        stroke: '#5F95FF',
        strokeWidth: 2,
        rx: 6,
        ry: 6
      },
      label: {
        text: task.name,
        fill: '#333333',
        fontSize: 14
      }
    },
    data: {
      name: task.name,
      taskType: task.taskType,
      executionOrder: 1,
      isConcurrent: false,
      waitForPrevious: true,
      skipConditions: [],
      config: task.config || {}
    }
  })
}

function getTaskIcon(taskType: string) {
  const icons: Record<string, any> = {
    api: Connection,
    mqtt: Message,
    delay: Timer,
    condition: Setting,
    script: Document,
    data: DataLine,
    error: Warning
  }
  return icons[taskType] || Connection
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

function addSkipCondition() {
  if (skipConditionInput.value.trim()) {
    nodeConfig.value.skipConditions.push(skipConditionInput.value.trim())
    skipConditionInput.value = ''
  }
}

function removeSkipCondition(index: number) {
  nodeConfig.value.skipConditions.splice(index, 1)
}

function applyNodeConfig() {
  if (!selectedNode.value || !graph.value) return

  try {
    nodeConfig.value.config = JSON.parse(configJson.value)
  } catch (e) {
    ElMessage.error('配置参数JSON格式错误')
    return
  }

  selectedNode.value.setData(nodeConfig.value)
  selectedNode.value.attr('label/text', nodeConfig.value.name)
  nodeConfigVisible.value = false
  ElMessage.success('节点配置已应用')
}

function deleteSelectedNode() {
  if (!selectedNode.value || !graph.value) return

  selectedNode.value.remove()
  nodeConfigVisible.value = false
  ElMessage.success('节点已从工作流中移除')
}

function saveWorkflow() {
  if (!graph.value) return

  const nodes = graph.value.getNodes().map(node => ({
    id: node.id,
    ...node.getData()
  }))

  const edges = graph.value.getEdges().map(edge => ({
    id: edge.id,
    fromNodeId: edge.getSourceCellId(),
    toNodeId: edge.getTargetCellId()
  }))

  const workflowName = '工作流-' + new Date().toLocaleString()
  const workflow = {
    id: 'workflow-' + Date.now(),
    name: workflowName,
    description: '通过工作流编辑器创建',
    nodeCount: nodes.length,
    nodes,
    edges,
    createdAt: new Date().toISOString()
  }

  savedWorkflows.value.push(workflow)
  ElMessage.success(`工作流 "${workflowName}" 已保存`)
}

function showWorkflowList() {
  workflowListVisible.value = true
}

function clearWorkflow() {
  if (!graph.value) return
  graph.value.clearCells()
  ElMessage.success('画布已清空')
}

async function startWorkflow() {
  if (!graph.value) return

  const nodes = graph.value.getNodes()
  if (nodes.length === 0) {
    ElMessage.warning('请先添加任务组件到画布')
    return
  }

  try {
    const workflowId = 'workflow-' + Date.now()
    await workflowStore.startWorkflow(workflowId, {})
    ElMessage.success('工作流启动成功')
  } catch (error: any) {
    ElMessage.error('工作流启动失败: ' + error.message)
  }
}

async function stopWorkflow() {
  try {
    await workflowStore.stopWorkflow('current-workflow')
    ElMessage.success('工作流已停止')
  } catch (error: any) {
    ElMessage.error('工作流停止失败: ' + error.message)
  }
}

function loadWorkflow(workflow: any) {
  ElMessageBox.confirm('加载工作流将清空当前画布，确定继续吗？', '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    if (!graph.value) return
    
    graph.value.clearCells()
    
    if (workflow.nodes && workflow.nodes.length > 0) {
      const container = document.getElementById('graph-container')
      if (!container) return

      const rect = container.getBoundingClientRect()
      
      workflow.nodes.forEach((node: any, index: number) => {
        const x = (index % 3) * 200 + 50
        const y = Math.floor(index / 3) * 100 + 50
        
        graph.value!.addNode({
          x,
          y,
          width: 120,
          height: 60,
          shape: 'rect',
          ports: {
            groups: {
              top: {
                position: 'top',
                attrs: {
                  circle: {
                    r: 6,
                    magnet: true,
                    stroke: '#5F95FF',
                    strokeWidth: 2,
                    fill: '#fff'
                  }
                }
              },
              right: {
                position: 'right',
                attrs: {
                  circle: {
                    r: 6,
                    magnet: true,
                    stroke: '#5F95FF',
                    strokeWidth: 2,
                    fill: '#fff'
                  }
                }
              },
              bottom: {
                position: 'bottom',
                attrs: {
                  circle: {
                    r: 6,
                    magnet: true,
                    stroke: '#5F95FF',
                    strokeWidth: 2,
                    fill: '#fff'
                  }
                }
              },
              left: {
                position: 'left',
                attrs: {
                  circle: {
                    r: 6,
                    magnet: true,
                    stroke: '#5F95FF',
                    strokeWidth: 2,
                    fill: '#fff'
                  }
                }
              }
            },
            items: [
              { id: 'top', group: 'top' },
              { id: 'right', group: 'right' },
              { id: 'bottom', group: 'bottom' },
              { id: 'left', group: 'left' }
            ]
          },
          attrs: {
            body: {
              fill: '#ffffff',
              stroke: '#5F95FF',
              strokeWidth: 2,
              rx: 6,
              ry: 6
            },
            label: {
              text: node.name,
              fill: '#333333',
              fontSize: 14
            }
          },
          data: node
        })
      })

      if (workflow.edges && workflow.edges.length > 0) {
        workflow.edges.forEach((edge: any) => {
          graph.value!.addEdge({
            source: edge.fromNodeId,
            target: edge.toNodeId,
            attrs: {
              line: {
                stroke: '#A2B1C3',
                strokeWidth: 2,
                targetMarker: {
                  name: 'classic',
                  size: 10
                }
              }
            }
          })
        })
      }
    }
    
    ElMessage.success(`工作流 "${workflow.name}" 已加载`)
  }).catch(() => {})
}

function editWorkflow(workflow: any) {
  loadWorkflow(workflow)
}

async function executeWorkflow(workflow: any) {
  try {
    await workflowStore.startWorkflow(workflow.id, {})
    ElMessage.success(`工作流 "${workflow.name}" 执行成功`)
  } catch (error: any) {
    ElMessage.error('工作流执行失败: ' + error.message)
  }
}

function deleteWorkflow(workflow: any) {
  ElMessageBox.confirm(`确定要删除工作流 "${workflow.name}" 吗？`, '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    const index = savedWorkflows.value.findIndex(w => w.id === workflow.id)
    if (index !== -1) {
      savedWorkflows.value.splice(index, 1)
      ElMessage.success('工作流已删除')
    }
  }).catch(() => {})
}

function formatDate(date: string) {
  return dayjs(date).format('YYYY-MM-DD HH:mm:ss')
}
</script>

<style scoped>
.workflow-editor {
  height: 100vh;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 100%;
}

.header-content h2 {
  margin: 0;
  font-size: 20px;
  color: #333;
}

.header-actions {
  display: flex;
  gap: 10px;
}

.task-palette {
  padding: 20px;
  height: 100%;
  overflow-y: auto;
  background: #fff;
  border-right: 1px solid #e0e0e0;
}

.task-palette h3 {
  margin: 0 0 15px 0;
  font-size: 16px;
  color: #333;
  border-bottom: 2px solid #5F95FF;
  padding-bottom: 10px;
}

.task-item {
  display: flex;
  align-items: center;
  padding: 12px;
  margin-bottom: 10px;
  background: #f5f7fa;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  cursor: grab;
  transition: all 0.3s;
  gap: 10px;
}

.task-item:hover {
  background: #e6f7ff;
  border-color: #5F95FF;
  transform: translateX(5px);
}

.task-item:active {
  cursor: grabbing;
}

.task-icon {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #5F95FF;
  border-radius: 4px;
  color: white;
  flex-shrink: 0;
}

.task-info {
  flex: 1;
  min-width: 0;
}

.task-name {
  font-size: 14px;
  font-weight: 500;
  color: #333;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-type {
  font-size: 12px;
  color: #909399;
}

.graph-container {
  width: 100%;
  height: calc(100vh - 60px);
  background: #f5f5f5;
}

.tags-container {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
}
</style>
