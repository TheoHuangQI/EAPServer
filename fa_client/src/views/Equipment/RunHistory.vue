<template>
  <div class="page-shell">
    <section class="filter-panel">
      <div class="filter-grid">
        <el-input v-model="pageInfo.eqpId" placeholder="设备ID" clearable @keyup.enter="handleSearch">
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>

        <el-input v-model="pageInfo.eqpType" placeholder="设备类型" clearable @keyup.enter="handleSearch">
          <template #prefix>
            <el-icon><Monitor /></el-icon>
          </template>
        </el-input>

        <el-select v-model="pageInfo.state" placeholder="作业状态" clearable>
          <el-option label="全部" value="" />
          <el-option label="未开始" :value="0" />
          <el-option label="进行中" :value="1" />
          <el-option label="已完成" :value="2" />
          <el-option label="失败" :value="3" />
        </el-select>

        <el-date-picker
          v-model="pageInfo.timeRange"
          type="datetimerange"
          range-separator="至"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
          value-format="YYYY-MM-DD HH:mm:ss"
          clearable
          class="range-picker"
        />

        <div class="toolbar">
          <el-button type="primary" @click="handleSearch">
            <el-icon><Search /></el-icon>
            查询
          </el-button>
          <el-button plain @click="handleReset">
            <el-icon><RefreshLeft /></el-icon>
            重置
          </el-button>
          <el-button plain :loading="loading" @click="fetchData">
            <el-icon><Refresh /></el-icon>
            刷新
          </el-button>
          <el-button type="success" plain @click="handleExport">
            <el-icon><Download /></el-icon>
            导出报表
          </el-button>
        </div>
      </div>
    </section>

    <section class="table-panel">
      <div class="table-toolbar">
        <div class="selection-text">
          共 <b>{{ filteredData.length }}</b> 条作业记录，当前分页按需渲染 <b>{{ paginatedData.length }}</b> 条
        </div>
        <div v-if="largeDataMode" class="status-badge status-badge--info">
          <span class="status-dot"></span>
          大数据模式
        </div>
      </div>

      <el-table
        v-loading="loading"
        :data="paginatedData"
        border
        stripe
        highlight-current-row
        row-key="id"
        max-height="calc(100vh - 300px)"
        @selection-change="handleSelectionChange"
        @sort-change="handleSortChange"
        @row-click="openDetail"
      >
        <el-table-column type="selection" width="54" align="center" reserve-selection fixed />
        <el-table-column label="序号" width="74" align="center" fixed>
          <template #default="scope">
            {{ (pageInfo.pageNo - 1) * pageInfo.pageSize + scope.$index + 1 }}
          </template>
        </el-table-column>
        <el-table-column prop="eqpId" label="设备号" width="130" fixed sortable="custom" show-overflow-tooltip />
        <el-table-column prop="eqpType" label="设备类型" width="150" sortable="custom" show-overflow-tooltip />
        <el-table-column prop="portName" label="PORT" width="120" align="center" sortable="custom">
          <template #default="scope">
            <copy-field :value="scope.row.portName || scope.row.portId" />
          </template>
        </el-table-column>
        <el-table-column prop="carrierId" label="CARRIER" width="150" sortable="custom" show-overflow-tooltip>
          <template #default="scope">
            <copy-field :value="scope.row.carrierId" />
          </template>
        </el-table-column>
        <el-table-column prop="lotId" label="LOT" min-width="180" sortable="custom" show-overflow-tooltip>
          <template #default="scope">
            <copy-field :value="scope.row.lotId" />
          </template>
        </el-table-column>
        <el-table-column prop="state" label="状态" width="120" align="center" sortable="custom">
          <template #default="scope">
            <span class="status-badge" :class="'status-badge--' + stateMeta(scope.row.state).tone">
              <span class="status-dot"></span>
              {{ stateMeta(scope.row.state).text }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="完成度" width="170" align="center">
          <template #default="scope">
            <el-progress
              :percentage="progressValue(scope.row)"
              :status="progressStatus(scope.row)"
              :stroke-width="8"
              striped
            />
          </template>
        </el-table-column>
        <el-table-column prop="startTime" label="开始时间" width="190" sortable="custom" />
        <el-table-column prop="endTime" label="结束时间" width="190" sortable="custom" />
        <el-table-column label="操作" width="96" align="center" fixed="right">
          <template #default="scope">
            <el-tooltip content="删除" placement="top">
              <el-button type="danger" circle size="small" @click.stop="handleDelete(scope.row)">
                <el-icon><Delete /></el-icon>
              </el-button>
            </el-tooltip>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination-bar">
        <el-pagination
          v-model:current-page="pageInfo.pageNo"
          v-model:page-size="pageInfo.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="filteredData.length"
          background
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange"
        />
      </div>
    </section>

    <el-drawer v-model="detailVisible" title="作业详情" size="420px">
      <div v-if="currentRow" class="detail-body">
        <el-descriptions :column="1" border>
          <el-descriptions-item label="设备号">{{ currentRow.eqpId || 'N/A' }}</el-descriptions-item>
          <el-descriptions-item label="设备类型">{{ currentRow.eqpType || 'N/A' }}</el-descriptions-item>
          <el-descriptions-item label="PORT">{{ currentRow.portName || currentRow.portId || 'N/A' }}</el-descriptions-item>
          <el-descriptions-item label="CARRIER">{{ currentRow.carrierId || 'N/A' }}</el-descriptions-item>
          <el-descriptions-item label="LOT">{{ currentRow.lotId || 'N/A' }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ stateMeta(currentRow.state).text }}</el-descriptions-item>
          <el-descriptions-item label="开始时间">{{ currentRow.startTime || 'N/A' }}</el-descriptions-item>
          <el-descriptions-item label="结束时间">{{ currentRow.endTime || 'N/A' }}</el-descriptions-item>
        </el-descriptions>

        <div class="detail-section">
          <h3>运行日志</h3>
          <el-timeline>
            <el-timeline-item
              v-for="(item, index) in timelineItems(currentRow)"
              :key="index"
              :timestamp="item.timestamp || ''"
              :type="item.type"
            >
              {{ item.text }}
            </el-timeline-item>
          </el-timeline>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<script>
import { h } from 'vue'
import { GetList, Delete } from '@/api/runHistory'

const CopyField = {
  props: {
    value: {
      type: [String, Number],
      default: ''
    }
  },
  methods: {
    copy() {
      const text = String(this.value || '')
      if (!text) {
        return
      }

      if (navigator.clipboard) {
        navigator.clipboard.writeText(text)
      }
      this.$message.success('已复制')
    }
  },
  render() {
    const text = this.value || 'N/A'
    return h('span', { class: 'copy-field' }, [
      h('span', { class: 'copy-field__text', title: String(text) }, String(text)),
      h('button', { class: 'copy-field__button', type: 'button', onClick: event => {
        event.stopPropagation()
        this.copy()
      } }, '复制')
    ])
  }
}

export default {
  components: { CopyField },
  data() {
    return {
      loading: false,
      selectedRows: [],
      tableData: [],
      total: 0,
      detailVisible: false,
      currentRow: null,
      sortInfo: {
        prop: '',
        order: ''
      },
      pageInfo: {
        eqpId: '',
        eqpType: '',
        state: '',
        timeRange: [],
        pageNo: 1,
        pageSize: 20
      }
    }
  },
  computed: {
    filteredData() {
      const [startTime, endTime] = this.pageInfo.timeRange || []
      const start = startTime ? Date.parse(startTime) : null
      const end = endTime ? Date.parse(endTime) : null
      let data = [...this.tableData]

      if (this.pageInfo.state !== '' && this.pageInfo.state !== null && this.pageInfo.state !== undefined) {
        data = data.filter(item => Number(item.state) === Number(this.pageInfo.state))
      }
      if (start || end) {
        data = data.filter(item => {
          const time = Date.parse(item.startTime || item.endTime || '')
          if (!Number.isFinite(time)) return false
          if (start && time < start) return false
          if (end && time > end) return false
          return true
        })
      }

      if (this.sortInfo.prop && this.sortInfo.order) {
        const direction = this.sortInfo.order === 'ascending' ? 1 : -1
        data.sort((a, b) => {
          const left = a[this.sortInfo.prop]
          const right = b[this.sortInfo.prop]
          if (left === right) return 0
          if (left === null || left === undefined) return -1 * direction
          if (right === null || right === undefined) return 1 * direction
          return String(left).localeCompare(String(right), 'zh-CN', { numeric: true }) * direction
        })
      }

      return data
    },
    paginatedData() {
      const start = (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize
      const end = this.pageInfo.pageNo * this.pageInfo.pageSize
      return this.filteredData.slice(start, end)
    },
    largeDataMode() {
      return this.filteredData.length >= 300
    }
  },
  created() {
    this.fetchData()
  },
  methods: {
    async fetchData() {
      this.loading = true
      try {
        const params = {
          ...this.pageInfo,
          startTime: this.pageInfo.timeRange && this.pageInfo.timeRange[0],
          endTime: this.pageInfo.timeRange && this.pageInfo.timeRange[1]
        }
        const res = await GetList(params)
        const list = res && res.data && Array.isArray(res.data.list) ? res.data.list : []
        this.tableData = list
        this.total = res && res.data && Number.isFinite(Number(res.data.total)) ? Number(res.data.total) : list.length
      } finally {
        this.loading = false
      }
    },
    handleSearch() {
      this.pageInfo.pageNo = 1
      this.fetchData()
    },
    handleReset() {
      this.pageInfo.eqpId = ''
      this.pageInfo.eqpType = ''
      this.pageInfo.state = ''
      this.pageInfo.timeRange = []
      this.pageInfo.pageNo = 1
      this.fetchData()
    },
    handleSortChange({ prop, order }) {
      this.sortInfo = { prop, order }
    },
    handleSelectionChange(selection) {
      this.selectedRows = selection.map(item => item.id)
    },
    handleSizeChange() {
      this.pageInfo.pageNo = 1
    },
    openDetail(row) {
      this.currentRow = row
      this.detailVisible = true
    },
    stateMeta(state) {
      const map = {
        0: { text: '未开始', tone: 'warning', detailText: '作业已创建，等待运行', timelineType: 'warning' },
        1: { text: '进行中', tone: 'info', detailText: '设备正在执行作业', timelineType: 'primary' },
        2: { text: '已完成', tone: 'success', detailText: '作业已完成', timelineType: 'success' },
        3: { text: '失败', tone: 'danger', detailText: '作业执行失败', timelineType: 'danger' }
      }
      return map[Number(state)] || map[2]
    },
    timelineItems(row) {
      if (!row) return []

      const startTime = row.startTime || ''
      const endTime = row.endTime || ''
      const state = Number(row.state)
      const items = [
        { text: '作业创建并进入队列', timestamp: startTime, type: 'primary' }
      ]

      if (state === 0) {
        items.push({ text: '等待设备执行', timestamp: startTime, type: 'warning' })
      } else {
        items.push({ text: '作业开始执行', timestamp: startTime, type: 'primary' })
        if (state === 1) {
          items.push({ text: '作业进行中', timestamp: '', type: 'primary' })
        } else if (state === 2) {
          items.push({ text: '作业已完成', timestamp: endTime, type: 'success' })
        } else if (state === 3) {
          items.push({ text: '作业执行失败', timestamp: endTime, type: 'danger' })
        } else {
          const meta = this.stateMeta(state)
          items.push({ text: meta.detailText, timestamp: endTime, type: meta.timelineType })
        }
      }

      return items
    },
    progressValue(row) {
      const map = {
        0: 10,
        1: 55,
        2: 100,
        3: 100
      }
      return map[Number(row.state)] || 100
    },
    progressStatus(row) {
      if (Number(row.state) === 2) return 'success'
      if (Number(row.state) === 3) return 'exception'
      return ''
    },
    handleDelete(row) {
      this.$confirm('确定删除该作业记录吗？', '删除确认', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Delete([row.id]).then(res => {
          if (res.return === 'OK') {
            this.$message.success('删除成功')
            this.fetchData()
          } else {
            this.$message.error(res.message)
          }
        })
      }).catch(() => {})
    },
    handleExport() {
      const rows = this.filteredData
      const headers = ['设备号', '设备类型', 'PORT', 'CARRIER', 'LOT', '状态', '完成度', '开始时间', '结束时间']
      const body = rows.map(row => [
        row.eqpId,
        row.eqpType,
        row.portName || row.portId,
        row.carrierId,
        row.lotId,
        this.stateMeta(row.state).text,
        this.progressValue(row) + '%',
        row.startTime,
        row.endTime
      ])
      this.downloadCsv('作业记录报表.csv', [headers, ...body])
    },
    downloadCsv(filename, rows) {
      const content = rows
        .map(row => row.map(value => `"${String(value ?? '').replace(/"/g, '""')}"`).join(','))
        .join('\n')
      const blob = new Blob(['\ufeff' + content], { type: 'text/csv;charset=utf-8;' })
      const link = document.createElement('a')
      link.href = URL.createObjectURL(blob)
      link.download = filename
      link.click()
      URL.revokeObjectURL(link.href)
    }
  }
}
</script>

<style lang="scss" scoped>
.page-shell {
  min-height: calc(100vh - 64px);
  padding: 16px;
  background: #f5f7fa;
  color: #182433;
  text-align: left;
  box-sizing: border-box;
}

.filter-panel,
.table-panel {
  border: 1px solid #dce3ec;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 1px 2px rgba(24, 36, 51, 0.05);
}

.filter-panel {
  padding: 14px;
  margin-bottom: 12px;
}

.filter-grid {
  display: grid;
  grid-template-columns: minmax(150px, 190px) minmax(150px, 190px) minmax(140px, 160px) minmax(330px, 1fr) auto;
  gap: 12px;
  align-items: center;
}

.range-picker {
  width: 100%;
}

.toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.table-panel {
  padding: 12px;
}

.table-toolbar,
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.table-toolbar {
  margin-bottom: 10px;
}

.selection-text {
  color: #596679;
  font-size: 13px;
}

.selection-text b {
  color: #1890ff;
}

.pagination-bar {
  justify-content: flex-end;
  position: sticky;
  bottom: 0;
  z-index: 5;
  margin-top: 12px;
  padding-top: 10px;
  background: #fff;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-height: 24px;
  padding: 3px 9px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 700;
  line-height: 1;
  white-space: nowrap;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
}

.status-badge--success {
  color: #13744d;
  background: #e9f7f0;
}

.status-badge--info {
  color: #1d5fa7;
  background: #e9f2ff;
}

.status-badge--warning {
  color: #8a5a00;
  background: #fff4dc;
}

.status-badge--danger {
  color: #b42318;
  background: #fff0ed;
}

:deep(.el-table th.el-table__cell) {
  background: #f5f7fa;
  color: #182433;
  font-weight: 800;
}

:deep(.el-table__row:hover > td.el-table__cell) {
  background: #f2f8ff;
}

:deep(.el-button),
:deep(.el-input__wrapper),
:deep(.el-select__wrapper) {
  border-radius: 6px;
}

:deep(.copy-field) {
  display: inline-flex;
  align-items: center;
  max-width: 100%;
  gap: 6px;
}

:deep(.copy-field__text) {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

:deep(.copy-field__button) {
  flex: 0 0 auto;
  border: none;
  border-radius: 5px;
  padding: 2px 6px;
  color: #1890ff;
  background: #e9f2ff;
  cursor: pointer;
  font-size: 12px;
}

.detail-body {
  text-align: left;
}

.detail-section {
  margin-top: 18px;
}

.detail-section h3 {
  margin: 0 0 12px;
  font-size: 15px;
  color: #182433;
}

@media (max-width: 1100px) {
  .filter-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 720px) {
  .filter-grid,
  .table-toolbar {
    grid-template-columns: 1fr;
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
