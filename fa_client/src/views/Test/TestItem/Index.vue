<template>
  <div class="app-container">
    <!-- 操作栏 -->
    <el-card class="filter-card" shadow="never">
      <el-row :gutter="20">
        <el-col :xs="24" :sm="16" :md="12" :lg="8">
          <el-input v-model="messageName" placeholder="名称" clearable @keyup.enter.native="handleSearch">
            <template v-slot:prefix>
<i  class="el-icon-search el-input__icon" />
</template>
          </el-input>
        </el-col>
        <el-col :xs="24" :sm="8" :md="12" :lg="16" class="operation-col">
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
      <el-table :data="tableData.slice((currentPage - 1) * pageSize, currentPage * pageSize)" style="width: 100%" stripe
        row-key="messageName"
        @selection-change="handleSelectionChange">
        <el-table-column type="selection" width="55" align="center" reserve-selection />
        <el-table-column :index="indexHandler" label="序号" type="index" width="50" align="center" />
        <el-table-column prop="messageName" label="名称" align="center" />
        <el-table-column prop="description" label="描述" />
        <el-table-column prop="stream" label="Stream" />
        <el-table-column prop="function" label="Function" />
        <el-table-column prop="direction" label="流向" />
        <el-table-column prop="wait" label="等待"  :formatter="formatBool" />
        <el-table-column prop="autoReply" label="自动回复" :formatter="formatBool" />
        <el-table-column prop="dataItemContent" label="发送内容" show-overflow-tooltip  class-name="message-content-cell"/>
        <el-table-column prop="replyStream" label="Reply Stream" />
        <el-table-column prop="replyFunction" label="Reply Function" />
        <el-table-column prop="replyDataItemContent" label="回复内容" show-overflow-tooltip  class-name="message-content-cell"/>
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
      <el-pagination class="pagination-wrapper" :current-page="currentPage" :page-sizes="[5, 10, 20]"
        :page-size="pageSize" :total="total" layout="total, sizes, prev, pager, next, jumper"
        @size-change="handleSizeChange" @current-change="handlePageChange" />
    </el-card>

    <!-- 编辑对话框 -->
    <Dialog :visible="showModal" @update:visible="showModal = $event" :is-edit="isEdit" :form="formData" @submit="handleDialogSubmit" />
  </div>
</template>
  
<script>
import { GetTestItemList, DeleteTestItem, EditTestItem, AddTestItem} from "@/api/Test"
import Dialog from "./Dialog.vue"
export default {
  components: { Dialog },
  data() {
    return {
      loading: false,
      messageName: '',
      selectedRows: [],
      showModal: false,
      isEdit: false,
      tableData: [
      ],
      formData: this.getDefaultForm(),
      currentPage: 1,
      pageSize: 10,
      total: 35,
    }
  },
  created() {
    this.fetchData()
  },
  methods: {
    formatBool(row, column) {
      return row[column.property] ? '✔️' : '❌';
    },
    getDefaultForm() {
      return {
        messageName: '',
        description: '',
        stream:null,
        function: null,
        direction:'',
        wait:0,
        autoReply:0,
        dataItemContent:'',
        replyStream:null,
        replyFunction: null,
        replyDataItemContent:'',
      }
    },
    indexHandler(index) {
      return (this.currentPage - 1) * this.pageSize + index + 1
    },
    async fetchData() {
      this.loading = true
      try {
        //debugger
        const res = await GetTestItemList(this.messageName)
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
      this.$confirm('确定删除测试项吗？', '警告', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        DeleteTestItem([row.messageName]).then(res => {
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
        DeleteTestItem(this.selectedRows).then(res => {
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
      this.selectedRows = selection.map(item => item.messageName)
    },
    handleSizeChange(val) {
      this.pageSize = val
      this.currentPage = 1
    },
    handlePageChange(val) {
      this.currentPage = val
    },
    handleDialogSubmit(formData) {
      this.loading = true
      try {
        if (this.isEdit) {
          const res = EditTestItem(formData).then(res => {
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
          const res = AddTestItem(formData).then(res => {
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

:deep(.message-content-cell) {
  .cell {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
}
</style>