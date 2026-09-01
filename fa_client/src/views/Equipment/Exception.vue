<template>
  <div class="page-shell">
    <section class="filter-panel">
      <div class="tab-row">
        <el-tabs v-model="activeTab" class="exception-tabs" @tab-change="handleTabChange">
          <el-tab-pane name="all">
            <template #label>
              <span class="tab-label">全部 <em>{{ counts.all }}</em></span>
            </template>
          </el-tab-pane>
          <el-tab-pane name="pending">
            <template #label>
              <span class="tab-label">未处理 <em>{{ counts.pending }}</em></span>
            </template>
          </el-tab-pane>
          <el-tab-pane name="processed">
            <template #label>
              <span class="tab-label">已处理 <em>{{ counts.processed }}</em></span>
            </template>
          </el-tab-pane>
          <el-tab-pane name="today">
            <template #label>
              <span class="tab-label">今日异常 <em>{{ counts.today }}</em></span>
            </template>
          </el-tab-pane>
        </el-tabs>
      </div>

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

        <el-input v-model="pageInfo.keyword" placeholder="异常内容" clearable @keyup.enter="handleSearch">
          <template #prefix>
            <el-icon><Warning /></el-icon>
          </template>
        </el-input>

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
            导出
          </el-button>
        </div>
      </div>
    </section>

    <section class="table-panel">
      <div class="table-toolbar">
        <div class="selection-text">
          已选择 <b>{{ selectedIds.length }}</b> 条异常，自动刷新每 10 秒执行一次
        </div>
        <div class="toolbar toolbar--right">
          <el-button size="small" type="success" :disabled="selectedIds.length === 0" @click="openBatchProcess">
            <el-icon><CircleCheck /></el-icon>
            批量处理
          </el-button>
          <el-button size="small" type="danger" plain :disabled="selectedIds.length === 0" @click="handleBatchDelete">
            <el-icon><Delete /></el-icon>
            批量删除
          </el-button>
        </div>
      </div>

      <el-table
        v-loading="loading"
        :data="paginatedData"
        border
        stripe
        highlight-current-row
        row-key="id"
        max-height="calc(100vh - 340px)"
        @selection-change="handleSelectionChange"
        @sort-change="handleSortChange"
      >
        <el-table-column type="selection" width="54" align="center" reserve-selection fixed />
        <el-table-column label="序号" width="74" align="center" fixed>
          <template #default="scope">
            {{ (pageInfo.pageNo - 1) * pageInfo.pageSize + scope.$index + 1 }}
          </template>
        </el-table-column>
        <el-table-column prop="eqpId" label="设备号" width="130" fixed sortable="custom" show-overflow-tooltip />
        <el-table-column prop="eqpType" label="设备类型" width="140" sortable="custom" show-overflow-tooltip />
        <el-table-column prop="portName" label="PORT" width="110" align="center" sortable="custom" show-overflow-tooltip />
        <el-table-column prop="lotId" label="LOT" min-width="150" sortable="custom" show-overflow-tooltip />
        <el-table-column prop="priority" label="优先级" width="116" align="center" sortable="custom">
          <template #default="scope">
            <span class="priority-badge" :class="'priority-badge--' + priorityMeta(scope.row).tone">
              {{ priorityMeta(scope.row).text }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="content" label="异常内容" min-width="320" sortable="custom">
          <template #default="scope">
            <el-tooltip :content="scope.row.content || 'N/A'" placement="top" effect="dark">
              <span class="content-text">{{ scope.row.content || 'N/A' }}</span>
            </el-tooltip>
          </template>
        </el-table-column>
        <el-table-column prop="errorTime" label="异常时间" width="190" sortable="custom" />
        <el-table-column prop="state" label="状态" width="120" align="center" sortable="custom">
          <template #default="scope">
            <span
              class="status-badge"
              :class="[
                'status-badge--' + statusMeta(scope.row.state).tone,
                { 'status-badge--pulse': Number(scope.row.state) === 0 }
              ]"
            >
              <span class="status-dot"></span>
              {{ statusMeta(scope.row.state).text }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="132" align="center" fixed="right">
          <template #default="scope">
            <div class="icon-actions">
              <el-tooltip content="处理" placement="top">
                <el-button
                  type="primary"
                  circle
                  size="small"
                  :disabled="Number(scope.row.state) !== 0"
                  @click="openProcessDialog(scope.row)"
                >
                  <el-icon><CircleCheck /></el-icon>
                </el-button>
              </el-tooltip>
              <el-tooltip content="删除" placement="top">
                <el-button type="danger" circle size="small" @click="handleDelete(scope.row)">
                  <el-icon><Delete /></el-icon>
                </el-button>
              </el-tooltip>
            </div>
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

    <el-dialog v-model="processDialogVisible" title="异常处理" width="520px">
      <el-form label-position="top">
        <el-form-item label="处理对象">
          <span class="dialog-tip">{{ processingIds.length }} 条异常</span>
        </el-form-item>
        <el-form-item label="处理结果 / 备注">
          <el-input
            v-model="processRemark"
            type="textarea"
            :rows="4"
            maxlength="300"
            show-word-limit
            placeholder="请输入处理说明"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="processDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="processLoading" @click="submitProcess">提交处理</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script>
import { GetList, Delete, Deletes, Edit } from '@/api/Exception'

const POLL_INTERVAL = 10000

export default {
  name: 'ExceptionRecordsPage',
  data() {
    return {
      loading: false,
      processLoading: false,
      selectedIds: [],
      tableData: [],
      total: 0,
      activeTab: 'all',
      pollTimer: null,
      knownPendingIds: new Set(),
      hasInitializedPolling: false,
      processDialogVisible: false,
      processRemark: '',
      processingIds: [],
      sortInfo: {
        prop: '',
        order: ''
      },
      pageInfo: {
        eqpId: '',
        eqpType: '',
        keyword: '',
        pageNo: 1,
        pageSize: 20
      }
    }
  },
  computed: {
    counts() {
      return {
        all: this.tableData.length,
        pending: this.tableData.filter(item => Number(item.state) === 0).length,
        processed: this.tableData.filter(item => Number(item.state) !== 0).length,
        today: this.tableData.filter(item => this.isToday(item.errorTime)).length
      }
    },
    filteredData() {
      const keyword = String(this.pageInfo.keyword || '').trim().toLowerCase()
      let data = [...this.tableData]

      if (this.activeTab === 'pending') {
        data = data.filter(item => Number(item.state) === 0)
      } else if (this.activeTab === 'processed') {
        data = data.filter(item => Number(item.state) !== 0)
      } else if (this.activeTab === 'today') {
        data = data.filter(item => this.isToday(item.errorTime))
      }

      if (keyword) {
        data = data.filter(item => String(item.content || '').toLowerCase().includes(keyword))
      }

      if (this.sortInfo.prop && this.sortInfo.order) {
        const direction = this.sortInfo.order === 'ascending' ? 1 : -1
        data.sort((a, b) => {
          const left = this.sortInfo.prop === 'priority' ? this.priorityMeta(a).weight : a[this.sortInfo.prop]
          const right = this.sortInfo.prop === 'priority' ? this.priorityMeta(b).weight : b[this.sortInfo.prop]
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
    }
  },
  created() {
    this.fetchData()
  },
  mounted() {
    this.startPolling()
  },
  activated() {
    this.startPolling()
  },
  deactivated() {
    this.stopPolling()
  },
  beforeUnmount() {
    this.stopPolling()
  },
  methods: {
    async fetchData(options = {}) {
      if (!options.silent) {
        this.loading = true
      }

      try {
        const res = await GetList({
          eqpId: this.pageInfo.eqpId,
          eqpType: this.pageInfo.eqpType,
          pageNo: this.pageInfo.pageNo,
          pageSize: this.pageInfo.pageSize
        })
        const list = res && res.data && Array.isArray(res.data.list) ? res.data.list : []
        this.tableData = list
        this.total = res && res.data && Number.isFinite(Number(res.data.total)) ? Number(res.data.total) : list.length
        this.handleRealtimeNotice(list)
      } finally {
        this.loading = false
      }
    },
    startPolling() {
      if (this.pollTimer) {
        return
      }

      this.pollTimer = setInterval(() => {
        this.fetchData({ silent: true })
      }, POLL_INTERVAL)
    },
    stopPolling() {
      if (!this.pollTimer) {
        return
      }

      clearInterval(this.pollTimer)
      this.pollTimer = null
    },
    handleRealtimeNotice(list) {
      const pendingRows = list.filter(item => Number(item.state) === 0)
      const nextIds = new Set(pendingRows.map(item => item.id))

      if (this.hasInitializedPolling) {
        const newRows = pendingRows.filter(item => !this.knownPendingIds.has(item.id))
        if (newRows.length > 0 && this.$notify) {
          this.$notify({
            title: '新异常提醒',
            message: `发现 ${newRows.length} 条新的未处理异常`,
            type: 'warning',
            duration: 4500
          })
        }
      }

      this.knownPendingIds = nextIds
      this.hasInitializedPolling = true
    },
    handleSearch() {
      this.pageInfo.pageNo = 1
      this.fetchData()
    },
    handleReset() {
      this.pageInfo.eqpId = ''
      this.pageInfo.eqpType = ''
      this.pageInfo.keyword = ''
      this.activeTab = 'all'
      this.pageInfo.pageNo = 1
      this.fetchData()
    },
    handleTabChange() {
      this.pageInfo.pageNo = 1
    },
    handleSortChange({ prop, order }) {
      this.sortInfo = { prop, order }
    },
    handleSizeChange() {
      this.pageInfo.pageNo = 1
    },
    handleSelectionChange(selection) {
      this.selectedIds = selection.map(item => item.id)
    },
    statusMeta(state) {
      return Number(state) === 0
        ? { text: '未处理', tone: 'danger' }
        : { text: '已处理', tone: 'success' }
    },
    priorityMeta(row) {
      const content = String(row.content || '').toUpperCase()
      if (Number(row.state) === 0 && /(ALID|ALARM|ERROR|FAIL|CRITICAL)/.test(content)) {
        return { text: '高', tone: 'high', weight: 3 }
      }
      if (Number(row.state) === 0) {
        return { text: '中', tone: 'middle', weight: 2 }
      }
      return { text: '低', tone: 'low', weight: 1 }
    },
    isToday(value) {
      if (!value) {
        return false
      }

      const date = new Date(value)
      const now = new Date()
      return date.getFullYear() === now.getFullYear() &&
        date.getMonth() === now.getMonth() &&
        date.getDate() === now.getDate()
    },
    openProcessDialog(row) {
      this.processingIds = [row.id]
      this.processRemark = ''
      this.processDialogVisible = true
    },
    openBatchProcess() {
      if (this.selectedIds.length === 0) {
        return
      }

      this.processingIds = [...this.selectedIds]
      this.processRemark = ''
      this.processDialogVisible = true
    },
    async submitProcess() {
      if (!this.processRemark.trim()) {
        this.$message.warning('请填写处理结果或备注')
        return
      }

      this.processLoading = true
      try {
        const res = await Edit(this.processingIds)
        if (res.return === 'OK') {
          this.tableData = this.tableData.map(item => this.processingIds.includes(item.id)
            ? { ...item, state: 1, processRemark: this.processRemark }
            : item
          )
          this.$message.success('处理成功')
          this.processDialogVisible = false
          await this.fetchData({ silent: true })
        } else {
          this.$message.error(res.message || '处理失败')
        }
      } finally {
        this.processLoading = false
      }
    },
    handleBatchDelete() {
      if (this.selectedIds.length === 0) {
        this.$message.warning('请至少选择一条记录')
        return
      }

      this.$confirm(`确定删除选中的 ${this.selectedIds.length} 条异常记录吗？`, '删除确认', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Deletes(this.selectedIds).then(res => {
          if (res.return === 'OK') {
            this.$message.success('删除成功')
            this.fetchData()
          } else {
            this.$message.error(res.message)
          }
        })
      }).catch(() => {})
    },
    handleDelete(row) {
      this.$confirm('确定删除该异常记录吗？', '删除确认', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Delete(row).then(res => {
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
      const headers = ['设备号', '设备类型', 'PORT', 'LOT', '优先级', '异常内容', '异常时间', '状态']
      const body = this.filteredData.map(row => [
        row.eqpId,
        row.eqpType,
        row.portName || row.portId,
        row.lotId,
        this.priorityMeta(row).text,
        row.content,
        row.errorTime,
        this.statusMeta(row.state).text
      ])
      this.downloadCsv('异常记录.csv', [headers, ...body])
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
  padding: 12px 14px 14px;
  margin-bottom: 12px;
}

.tab-row {
  margin-bottom: 8px;
}

.exception-tabs :deep(.el-tabs__header) {
  margin: 0;
}

.tab-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-weight: 700;
}

.tab-label em {
  min-width: 22px;
  padding: 1px 7px;
  border-radius: 999px;
  color: #1890ff;
  background: #e9f2ff;
  font-style: normal;
  text-align: center;
}

.filter-grid {
  display: grid;
  grid-template-columns: minmax(160px, 220px) minmax(160px, 220px) minmax(180px, 1fr) auto;
  gap: 12px;
  align-items: center;
}

.toolbar,
.icon-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.toolbar--right {
  justify-content: flex-end;
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

.content-text {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: middle;
  white-space: nowrap;
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

.status-badge,
.priority-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-height: 24px;
  padding: 3px 9px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 800;
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

.status-badge--danger {
  color: #b42318;
  background: #fff0ed;
}

.status-badge--pulse {
  animation: pulseWarning 1.8s ease-in-out infinite;
}

.priority-badge--high {
  color: #fff;
  background: #ff4d4f;
}

.priority-badge--middle {
  color: #8a5a00;
  background: #fff4dc;
}

.priority-badge--low {
  color: #5f6b7a;
  background: #f0f3f7;
}

.dialog-tip {
  color: #596679;
  font-weight: 700;
}

@keyframes pulseWarning {
  0% {
    box-shadow: 0 0 0 0 rgba(255, 77, 79, 0.32);
  }
  70% {
    box-shadow: 0 0 0 8px rgba(255, 77, 79, 0);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(255, 77, 79, 0);
  }
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
:deep(.el-input__wrapper) {
  border-radius: 6px;
}

@media (max-width: 980px) {
  .filter-grid {
    grid-template-columns: 1fr;
  }

  .table-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .toolbar--right {
    justify-content: flex-start;
  }
}
</style>
