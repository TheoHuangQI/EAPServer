<template>
  <div class="app-container">
    <!-- 操作栏 -->
    <el-card class="filter-card" shadow="never">
      <el-row :gutter="20">
        <el-col :span="4">
          <el-input v-model="pageInfo.carrierId" placeholder="载具编号" clearable @keyup.enter.native="handleSearch">
            <template v-slot:prefix>
<i  class="el-icon-search el-input__icon" />
</template>
          </el-input>
        </el-col>
        <el-col :span="4">
        <el-select v-model="pageInfo.in_use" placeholder="使用状态" clearable>
          <el-option  v-for="option in stateOptions" :key="option.value" :label="option.label" :value="option.value" />
        </el-select>
      </el-col>
        <el-col :span="16" class="operation-col">
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
       <el-table 
       :data="tableData.slice((pageInfo.pageNo-1)*pageInfo.pageSize,pageInfo.pageNo*pageInfo.pageSize)"
       style="width: 100%"  stripe @selection-change="handleSelectionChange" row-key="id">
        <el-table-column type="selection" width="55" align="center" reserve-selection />
        <el-table-column :index="indexHandler" label="序号" type="index" width="80" align="center" />
        <el-table-column prop="carrierId" label="载具编号" align="center" />
        <el-table-column prop="size" label="尺寸" />
        <el-table-column prop="in_use" label="使用状态">
          <template #default="scope">
            <el-tag :type="scope.row.in_use === 1 ? 'success' : 'warning'">
              {{ scope.row.in_use === 1 ? '空闲' : '使用中' }}
            </el-tag>
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
import { Get, Add,Edit,Delete} from "@/api/carrier"
import Dialog from "./Dialog.vue"
export default {
  components: { Dialog },
  data() {
    return {
      stateOptions: [
        { value: 1, label: '空闲' },
        { value: 2, label: '使用中' }
      ],
      pageInfo: {
        carrierId: '',
        in_use: null,
        pageNo: 1,
        pageSize: 10,
      },
      total:0,
      loading: false,
      selectedRows: [],
      showModal: false,
      isEdit: false,
      tableData: [
      ],
      formData: this.getDefaultForm(),
    }
  },
  created() {
    this.fetchData()
  },
  methods: {
    getDefaultForm() {
      return {
        carrierId: '',
        size: '',
        in_use: 1
      }
    },
    indexHandler(index) {
      return (this.pageInfo.pageNo - 1) * this.pageInfo.pageSize + index + 1
    },
    async fetchData() {
      this.loading = true
      try {
        const res = await Get(this.pageInfo)
        this.tableData = res.data.list
        this.total = res.data.total
      } catch (error) {
        console.error('获取载具数据失败:', error)
        this.$message.error('数据加载失败，请检查后端服务是否运行或网络连接是否正常。')
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
      this.$confirm('确定删除载具吗？', '警告', {
        type: 'warning',
        confirmButtonText: '确认删除',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger'
      }).then(() => {
        Delete([row.id]).then(res=>{
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
        Delete(this.selectedRows).then(res=>{
          if (res.return == "OK") {
              this.$message.success('批量删除成功')
              this.fetchData()
            }
            else {
              this.$message.error(res.message)
            }
        });
      }).catch(() => { })
    },
    handleSelectionChange(selection) {
      this.selectedRows = selection.map(item => item.id)
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