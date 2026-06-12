<template>
  <div class="app-container">
    <!-- 操作栏 -->
    <el-card class="filter-card" shadow="never">
      <el-row :gutter="20">
        <el-col :xs="4" :sm="4" :md="4" :lg="4">
          <el-input v-model="pageInfo.eqpId" placeholder="设备ID" clearable @keyup.enter.native="handleSearch">
          </el-input>
        </el-col>
        <el-col :xs="4" :sm="4" :md="4" :lg="4">
          <el-input v-model="pageInfo.eqpType" placeholder="设备类型" clearable @keyup.enter.native="handleSearch">
          </el-input>
        </el-col>
        <el-col :xs="8" :sm="8" :md="8" :lg="8" class="operation-col">
          <el-button-group>
            <el-button type="primary" @click="handleSearch">
              <i class="el-icon-search" /> 查询
            </el-button>
            <el-button type="success" @click="handleAdd">
              <i class="el-icon-plus" /> 新增
            </el-button>
            <el-button type="danger" :disabled="selectedRows.length === 0" @click="handleBatchDelete">
              <i class="el-icon-delete" /> 批量删除
            </el-button>
          </el-button-group>
        </el-col>
      </el-row>
    </el-card>

    <!-- 数据表格 -->
    <el-card class="table-card" shadow="never">
      <el-table :data="tableData.slice((pageInfo.pageNo - 1) * pageInfo.pageSize, pageInfo.pageNo * pageInfo.pageSize)" style="width: 100%" stripe
        @selection-change="handleSelectionChange" row-key="eqpId">
        <el-table-column type="selection" width="55" align="center" reserve-selection/>
        <el-table-column :index="indexHandler" label="序号" type="index" width="80" align="center" />
        <el-table-column prop="eqpId" label="设备号" align="center" />
        <el-table-column prop="eqpType" label="设备类型" />
        <el-table-column prop="enable" label="启用">
          <template #default="scope">
            <span v-if="scope.row.enable === 0">❌</span>
            <span v-else>✔️</span>
          </template>
        </el-table-column>
        <el-table-column prop="eqpStatus" label="设备状态">
          <template #default="scope">
            <span v-if="isConnected(scope.row.eqpStatus)">Connected</span>
            <span v-else>DisConnected</span>
          </template>
        </el-table-column>
        <el-table-column prop="eapStatus" label="EAP状态">
          <template #default="scope">
            <span v-if="isConnected(scope.row.eapStatus)">Connected</span>
            <span v-else>DisConnected</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="150" align="center">
          <template v-slot="scope">
            <el-tooltip content="编辑" placement="top">
              <el-button type="primary" icon="el-icon-edit" circle size="small" @click="handleEdit(scope.row)" />
            </el-tooltip>
            <el-tooltip content="删除" placement="top">
              <el-button type="danger" icon="el-icon-delete" circle size="small" @click="handleDelete(scope.row)" />
            </el-tooltip>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
      <el-pagination class="pagination-wrapper" :current-page="pageInfo.pageNo" :page-sizes="[5, 10, 20]"
        :page-size="pageInfo.pageSize" :total="total" layout="total, sizes, prev, pager, next, jumper"
        @size-change="handleSizeChange" @current-change="handlePageChange" />
    </el-card>

    <!-- 编辑对话框 -->
    <Dialog v-model:visible="showModal" :is-edit="isEdit" :form="formData" @submit="handleDialogSubmit" />
  </div>
</template>
  
<script>
import * as signalR from '@microsoft/signalr'
import { GetList, Add, Edit, Delete,Deletes} from "@/api/equipment"

import Dialog from "./Dialog.vue"
export default {
  components: { Dialog },
  data() {
    return {
      loading: false,
      selectedRows: [],
      showModal: false,
      isEdit: false,
      tableData: [
      ],
      formData: this.getDefaultForm(),
      total: 0,
      connection: null,
      pageInfo: {
        eqpId: '',
        eqpType: '',
        pageNo: 1,
        pageSize: 10,
      },

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
        enable:''
      }
    },
    indexHandler(index) {
      return (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize + index + 1
    },
    isConnected(status) {
      return Number(status) === 1
    },
    initSignalR() {
      if (this.connection) {
        return
      }
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl("https://localhost:7255/logHub", {
          skipNegotiation: true,
          transport: signalR.HttpTransportType.WebSockets,
          withCredentials: true
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build()

      this.connection.on("ReceiveLog", this.handleRealtimeLog)
      this.connection.onreconnected(() => {
        this.fetchData()
      })
      this.connection.start()
        .catch(err => console.error("SignalR Connection Error: ", err))
    },
    stopSignalR() {
      if (!this.connection) {
        return
      }
      this.connection.off("ReceiveLog", this.handleRealtimeLog)
      this.connection.stop().catch(() => {})
      this.connection = null
    },
    handleRealtimeLog(log) {
      if (!log || (log.type !== "EqpState" && log.type !== "EapState")) {
        return
      }
      const index = this.tableData.findIndex(item => item.eqpId === log.eqpId)
      if (index < 0) {
        return
      }
      const field = log.type === "EqpState" ? "eqpStatus" : "eapStatus"
      this.tableData.splice(index, 1, {
        ...this.tableData[index],
        [field]: log.message
      })
    },
    async fetchData() {
      this.loading = true
      try {
        const res = await GetList(this.pageInfo)
        this.tableData = res.data.list
        this.total = res.data.total
      } finally {
        this.loading = false
      }
    },
    handleSearch() {
      this.fetchData()
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
    handleDelete(row) {
      this.$confirm('确定删除设备吗？', '警告', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Delete(row).then(res => {
          if (res.return == "OK") {
            this.$message.success('删除成功')
            this.fetchData()
          }
          else {
            this.$message.error(res.message)
          }
        });
      }).catch(() => { })
    },
    handleBatchDelete() {
      if (this.selectedRows.length === 0) {
        this.$message.warning('请至少选择一条记录')
        return
      }
      this.$confirm(`确定删除选中的 ${this.selectedRows.length} 条记录吗？`, '警告', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Deletes(this.this.selectedRows).then(res => {
          if (res.return == "OK") {
            this.$message.success('删除成功')
            this.fetchData()
          }
          else {
            this.$message.error(res.message)
          }
        });
      }).catch(() => { })
    },
    handleSelectionChange(selection) {
      this.selectedRows = selection.map(item => item.eqpId)
    },
    handleSizeChange(val) {
      this.pageInfo.pageSize = val
      this.pageInfo.pageNo = 1
    },
    handlePageChange(val) {
      this.pageInfo.pageNo = val
    },
    handleDialogSubmit(formData) {
      this.loading = true
      try {
        if (this.isEdit) {
          const res = Edit(formData).then(res => {
            if (res.return == "OK") {
              this.$message.success('修改成功')
              this.fetchData()
            }
            else {
              this.$message.error(res.message)
            }
          })
        }
        else {
          const res = Add(formData).then(res => {
            if (res.return == "OK") {
              this.$message.success('添加成功')
              this.fetchData()
            }
            else {
              this.$message.error(res.message)
            }
          })
        }

      } finally {
        this.loading = false
      }
    }
  }
}
</script>
  
<style lang="scss" scoped>
.app-container {
  padding: 20px;
  background-color: #f5f7fa;

  .filter-card {
    margin-bottom: 20px;
    border-radius: 8px;
    border: 1px solid #ebeef5;

    .operation-col {
      text-align: left;

      @media (max-width: 768px) {
        text-align: left;
        margin-top: 10px;
      }
    }
  }

  .table-card {
    border-radius: 8px;
    border: 1px solid #ebeef5;

    :deep() .el-table {
      th {
        background-color: #f5f7fa;
      }
    }

    .pagination-wrapper {
      margin-top: 20px;
      text-align: right;
    }
  }

  .el-button-group {
    .el-button {
      &+.el-button {
        margin-left: 8px;
      }
    }
  }
}

.el-dialog {
  border-radius: 8px;

  .el-form-item {
    margin-bottom: 22px;
  }
}
</style>
