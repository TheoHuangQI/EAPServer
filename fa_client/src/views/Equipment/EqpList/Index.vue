<template>
  <div class="page-shell">
    <section class="filter-panel">
      <div class="filter-grid">
        <el-input
          v-model="pageInfo.eqpId"
          placeholder="设备ID"
          clearable
          @keyup.enter="handleSearch"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>

        <el-input
          v-model="pageInfo.eqpType"
          placeholder="设备类型"
          clearable
          @keyup.enter="handleSearch"
        >
          <template #prefix>
            <el-icon><Monitor /></el-icon>
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
          <el-button type="success" @click="handleAdd">
            <el-icon><Plus /></el-icon>
            新增
          </el-button>
        </div>
      </div>
    </section>

    <section class="summary-grid">
      <article class="metric-card metric-card--info">
        <span>总设备数</span>
        <strong>{{ stats.total }}</strong>
      </article>
      <article class="metric-card metric-card--success">
        <span>在线设备</span>
        <strong>{{ stats.online }}</strong>
      </article>
      <article class="metric-card metric-card--danger">
        <span>离线设备</span>
        <strong>{{ stats.offline }}</strong>
      </article>
      <article class="metric-card metric-card--neutral">
        <span>已启用</span>
        <strong>{{ stats.enabled }}</strong>
      </article>
    </section>

    <section class="table-panel">
      <div class="table-toolbar">
        <div class="selection-text">
          已选择 <b>{{ selectedDevices.length }}</b> 台设备
        </div>
        <div class="toolbar toolbar--right">
          <el-button size="small" plain :disabled="selectedDevices.length === 0" @click="handleBatchEnable(1)">
            <el-icon><CircleCheck /></el-icon>
            批量启用
          </el-button>
          <el-button size="small" plain :disabled="selectedDevices.length === 0" @click="handleBatchEnable(0)">
            <el-icon><CircleClose /></el-icon>
            批量禁用
          </el-button>
          <el-button size="small" type="danger" plain :disabled="selectedDevices.length === 0" @click="handleBatchDelete">
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
        row-key="eqpId"
        max-height="calc(100vh - 360px)"
        @selection-change="handleSelectionChange"
        @sort-change="handleSortChange"
      >
        <el-table-column type="selection" width="54" align="center" reserve-selection fixed />
        <el-table-column :index="indexHandler" label="序号" type="index" width="74" align="center" fixed sortable="custom" />
        <el-table-column prop="eqpId" label="设备号" min-width="150" fixed sortable="custom" show-overflow-tooltip />
        <el-table-column prop="eqpType" label="设备类型" min-width="150" sortable="custom" show-overflow-tooltip />
        <el-table-column prop="enable" label="启用" width="110" align="center" sortable="custom">
          <template #default="scope">
            <el-switch
              :model-value="isEnabled(scope.row.enable)"
              active-text="启用"
              inactive-text="禁用"
              inline-prompt
              :loading="scope.row.__switching"
              @change="value => handleEnableChange(scope.row, value)"
            />
          </template>
        </el-table-column>
        <el-table-column prop="eqpStatus" label="设备状态" width="150" align="center" sortable="custom">
          <template #default="scope">
            <span class="status-badge" :class="'status-badge--' + statusMeta(scope.row.eqpStatus).tone">
              <span class="status-dot"></span>
              {{ statusMeta(scope.row.eqpStatus).text }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="eapStatus" label="EAP状态" width="150" align="center" sortable="custom">
          <template #default="scope">
            <span class="status-badge" :class="'status-badge--' + statusMeta(scope.row.eapStatus).tone">
              <span class="status-dot"></span>
              {{ statusMeta(scope.row.eapStatus).text }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="148" align="center" fixed="right">
          <template #default="scope">
            <div class="icon-actions">
              <el-tooltip content="编辑" placement="top">
                <el-button type="primary" circle size="small" @click="handleEdit(scope.row)">
                  <el-icon><Edit /></el-icon>
                </el-button>
              </el-tooltip>
              <el-tooltip content="删除" placement="top">
                <el-button type="danger" circle size="small" @click="handleDelete(scope.row)">
                  <el-icon><Delete /></el-icon>
                </el-button>
              </el-tooltip>
              <el-dropdown trigger="click" @command="command => handleMoreCommand(command, scope.row)">
                <el-button circle size="small">
                  <el-icon><MoreFilled /></el-icon>
                </el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="refresh">刷新列表</el-dropdown-item>
                    <el-dropdown-item command="copy">复制设备ID</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </div>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination-bar">
        <el-pagination
          v-model:current-page="pageInfo.pageNo"
          v-model:page-size="pageInfo.pageSize"
          :page-sizes="[10, 20, 50, 100]"
          :total="sortedData.length"
          background
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange"
          @current-change="handlePageChange"
        />
      </div>
    </section>

    <EquipmentDialog v-model:visible="showModal" :is-edit="isEdit" :form="formData" @submit="handleDialogSubmit" />
  </div>
</template>

<script>
import * as signalR from '@microsoft/signalr'
import { GetList, Add, Edit, Delete, Deletes } from '@/api/equipment'
import EquipmentDialog from './Dialog.vue'

export default {
  name: 'EquipmentListPage',
  components: { EquipmentDialog },
  data() {
    return {
      loading: false,
      selectedDevices: [],
      showModal: false,
      isEdit: false,
      tableData: [],
      formData: this.getDefaultForm(),
      total: 0,
      connection: null,
      sortInfo: {
        prop: '',
        order: ''
      },
      pageInfo: {
        eqpId: '',
        eqpType: '',
        pageNo: 1,
        pageSize: 10
      }
    }
  },
  computed: {
    sortedData() {
      const data = [...this.tableData]
      if (!this.sortInfo.prop || !this.sortInfo.order) {
        return data
      }

      const direction = this.sortInfo.order === 'ascending' ? 1 : -1
      return data.sort((a, b) => {
        const left = a[this.sortInfo.prop]
        const right = b[this.sortInfo.prop]
        if (left === right) return 0
        if (left === null || left === undefined) return -1 * direction
        if (right === null || right === undefined) return 1 * direction
        return String(left).localeCompare(String(right), 'zh-CN', { numeric: true }) * direction
      })
    },
    paginatedData() {
      const start = (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize
      const end = this.pageInfo.pageNo * this.pageInfo.pageSize
      return this.sortedData.slice(start, end)
    },
    stats() {
      const total = this.tableData.length || this.total
      const online = this.tableData.filter(item => this.isConnected(item.eqpStatus)).length
      const enabled = this.tableData.filter(item => this.isEnabled(item.enable)).length

      return {
        total,
        online,
        offline: Math.max(total - online, 0),
        enabled
      }
    }
  },
  created() {
    this.fetchData()
  },
  mounted() {
    this.initSignalR()
  },
  beforeUnmount() {
    this.stopSignalR()
  },
  methods: {
    getDefaultForm() {
      return {
        eqpId: '',
        eqpType: '',
        enable: 1
      }
    },
    indexHandler(index) {
      return (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize + index + 1
    },
    isConnected(status) {
      return Number(status) === 1
    },
    isEnabled(enable) {
      return Number(enable) !== 0
    },
    statusMeta(status) {
      return this.isConnected(status)
        ? { text: 'Connected', tone: 'success' }
        : { text: 'DisConnected', tone: 'danger' }
    },
    initSignalR() {
      if (this.connection) {
        return
      }

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl('https://localhost:7255/logHub', {
          skipNegotiation: true,
          transport: signalR.HttpTransportType.WebSockets,
          withCredentials: true
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build()

      this.connection.on('ReceiveLog', this.handleRealtimeLog)
      this.connection.onreconnected(() => {
        this.fetchData()
      })
      this.connection.start()
        .catch(err => console.error('SignalR Connection Error: ', err))
    },
    stopSignalR() {
      if (!this.connection) {
        return
      }

      this.connection.off('ReceiveLog', this.handleRealtimeLog)
      this.connection.stop().catch(() => {})
      this.connection = null
    },
    handleRealtimeLog(log) {
      if (!log || (log.type !== 'EqpState' && log.type !== 'EapState')) {
        return
      }

      const index = this.tableData.findIndex(item => item.eqpId === log.eqpId)
      if (index < 0) {
        return
      }

      const field = log.type === 'EqpState' ? 'eqpStatus' : 'eapStatus'
      this.tableData.splice(index, 1, {
        ...this.tableData[index],
        [field]: log.message
      })
    },
    async fetchData() {
      this.loading = true
      try {
        const res = await GetList(this.pageInfo)
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
      this.pageInfo.pageNo = 1
      this.fetchData()
    },
    handleSortChange({ prop, order }) {
      this.sortInfo = { prop, order }
    },
    handleAdd() {
      this.isEdit = false
      this.formData = this.getDefaultForm()
      this.showModal = true
    },
    handleEdit(row) {
      this.isEdit = true
      this.showModal = true
      this.formData = { ...row }
    },
    async handleEnableChange(row, enabled) {
      row.__switching = true
      const nextEnable = enabled ? 1 : 0
      const previousEnable = row.enable

      try {
        const res = await Edit({ ...row, enable: nextEnable })
        if (res.return === 'OK') {
          row.enable = nextEnable
          this.$message.success(enabled ? '设备已启用' : '设备已禁用')
        } else {
          row.enable = previousEnable
          this.$message.error(res.message || '操作失败')
        }
      } catch (error) {
        row.enable = previousEnable
        this.$message.error('操作失败')
      } finally {
        row.__switching = false
      }
    },
    async handleBatchEnable(enable) {
      if (this.selectedDevices.length === 0) {
        return
      }

      this.loading = true
      try {
        const requests = this.selectedDevices.map(row => Edit({ ...row, enable }))
        await Promise.all(requests)
        this.$message.success(enable === 1 ? '批量启用完成' : '批量禁用完成')
        await this.fetchData()
      } finally {
        this.loading = false
      }
    },
    handleDelete(row) {
      this.$confirm(`确定删除设备 ${row.eqpId || ''} 吗？`, '删除确认', {
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
    handleBatchDelete() {
      if (this.selectedDevices.length === 0) {
        this.$message.warning('请至少选择一台设备')
        return
      }

      const ids = this.selectedDevices.map(item => item.eqpId)
      this.$confirm(`确定删除选中的 ${ids.length} 台设备吗？`, '删除确认', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Deletes(ids).then(res => {
          if (res.return === 'OK') {
            this.$message.success('删除成功')
            this.fetchData()
          } else {
            this.$message.error(res.message)
          }
        })
      }).catch(() => {})
    },
    handleSelectionChange(selection) {
      this.selectedDevices = selection
    },
    handleSizeChange() {
      this.pageInfo.pageNo = 1
    },
    handlePageChange() {},
    handleDialogSubmit(formData) {
      this.loading = true
      const request = this.isEdit ? Edit(formData) : Add(formData)

      request.then(res => {
        if (res.return === 'OK') {
          this.$message.success(this.isEdit ? '修改成功' : '添加成功')
          this.showModal = false
          this.fetchData()
        } else {
          this.$message.error(res.message)
        }
      }).finally(() => {
        this.loading = false
      })
    },
    handleMoreCommand(command, row) {
      if (command === 'refresh') {
        this.fetchData()
      }
      if (command === 'copy') {
        this.copyText(row.eqpId || '')
      }
    },
    copyText(text) {
      if (!text) {
        return
      }

      if (navigator.clipboard) {
        navigator.clipboard.writeText(text)
      }
      this.$message.success('已复制')
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
.table-panel,
.metric-card {
  border: 1px solid #dce3ec;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 1px 2px rgba(24, 36, 51, 0.05);
}

.filter-panel {
  padding: 14px;
}

.filter-grid {
  display: grid;
  grid-template-columns: minmax(180px, 240px) minmax(180px, 240px) minmax(360px, 1fr);
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

.summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(140px, 1fr));
  gap: 12px;
  margin: 12px 0;
}

.metric-card {
  min-height: 78px;
  padding: 12px;
  border-left: 4px solid #8a96a8;
  box-sizing: border-box;
}

.metric-card span {
  color: #596679;
  font-size: 12px;
  font-weight: 700;
}

.metric-card strong {
  display: block;
  margin-top: 10px;
  color: #162235;
  font-size: 26px;
  line-height: 1;
}

.metric-card--success {
  border-left-color: #52c41a;
}

.metric-card--info {
  border-left-color: #1890ff;
}

.metric-card--danger {
  border-left-color: #ff4d4f;
}

.metric-card--neutral {
  border-left-color: #8a96a8;
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

:deep(.el-button) {
  border-radius: 6px;
}

:deep(.el-input__wrapper) {
  border-radius: 6px;
}

@media (max-width: 960px) {
  .filter-grid,
  .summary-grid {
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
