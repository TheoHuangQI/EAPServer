<template>
    <div class="app-container">
      <!-- 操作栏 -->
      <el-card class="filter-card" shadow="never">
        <el-row :gutter="20">
          <el-col :span="4">
            <el-input v-model="batchId" placeholder="Batch Id" clearable @keyup.enter.native="handleSearch">
              <i slot="prefix" class="el-icon-search el-input__icon" />
            </el-input>
          </el-col>
          <el-col :span="20" class="operation-col">
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
         :data="tableData.slice((currentPage-1)*pageSize,currentPage*pageSize)"
         style="width: 100%" stripe @selection-change="handleSelectionChange" row-key="batchId">
          <el-table-column type="selection" width="55" align="center" reserve-selection/>
          <el-table-column :index="indexHandler" label="序号" type="index" width="80" align="center" />
          <el-table-column prop="batchId" label="Batch ID" align="center" />
          <el-table-column prop="tid" label="TID"/>
          <el-table-column prop="eqpId" label="设备号" />
          <el-table-column prop="carrierId" label="料盒号"/>
          <el-table-column prop="operator" label="操作员"/>
          <el-table-column prop="batchSize" label="Batch Size"/>
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
      <Dialog v-model:visible="showModal" :is-edit="isEdit" :form="formData" @submit="handleDialogSubmit" />
    </div>
  </template>
  
  <script>
  import { GetBatchList, AddBatch,EditBatch,DeleteBatch,DeleteBatchs} from "@/api/batch"

  import Dialog from "./Dialog.vue"
  export default {
    components: { Dialog },
    data() {
      return {
        loading: false,
        batchId: '',
        selectedRows: [],
        showModal: false,
        isEdit: false,
        tableData: [
        ],
        formData: this.getDefaultForm(),
        currentPage: 1,
        pageSize: 10,
        total: 0,
      }
    },
    created() {
      this.fetchData()
    },
    methods: {
      getDefaultForm() {
        return {
          batchId: '',
          tid: '',
          eqpId:'',
          carrierId:'',
          operator:'',
          batchSize:0,
          lots:[]
        }
      },
      indexHandler(index) {
        return (this.currentPage - 1) * this.pageSize + index + 1
      },
      async fetchData() {
        this.loading = true
        try {
          const res = await GetBatchList(this.batchId)
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
        this.$confirm('确定删除Batch吗？', '警告', {
          type: 'warning',
          confirmButtonText: '确认删除',
          cancelButtonText: '取消',
          confirmButtonClass: 'el-button--danger'
        }).then(() => {
          DeleteBatch(row).then(res=>{
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
          DeleteBatchs(this.selectedRows).then(res=>{
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
        this.selectedRows = selection.map(item => item.batchId)
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
            const res = EditBatch(formData).then(res => {
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
            const res = AddBatch(formData).then(res => {
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