<template>
  <!-- 设备选择对话框 -->
  <el-dialog 
  :title="isEdit ? 'Batch修改' : '新增Batch'"
  :visible.sync="dialogVisible" 
  width="960px" 
  :close-on-click-modal="false"
  @close="handleClose">
    <!-- 主内容区 -->
    <div class="dialog-container">
      <el-row :gutter="24">
        <!-- 左侧表单列 -->
        <el-col :span="12">
          <div class="form-section">
            <el-form label-position="left">
              <!-- 设备选择 -->
              <el-form-item label="设备选择" class="form-item--required">
                <el-select v-model="formData.eqpId" placeholder="请选择设备" clearable class="full-width">
                  <el-option v-for="device in devices" :key="device" :label="device" :value="device" />
                </el-select>
              </el-form-item>

              <!-- 载具选择 -->
              <el-form-item label="载具选择" class="form-item--required">
                <el-select v-model="formData.carrierId" placeholder="请选择载具" :disabled="!formData.eqpId" clearable
                  class="full-width">
                  <el-option v-for="carrier in carriers" :key="carrier" :label="carrier" :value="carrier" />
                </el-select>
              </el-form-item>

              <!-- 操作员输入 -->
              <el-form-item label="操作员" class="form-item--required">
                <el-input v-model="formData.operator" placeholder="请输入操作员工号" prefix-icon="el-icon-user" />
              </el-form-item>
            </el-form>
          </div>
        </el-col>

        <!-- 右侧 Lot 选择列 -->
        <el-col :span="12">
          <div class="lot-section">
            <el-form label-position="left">
              <el-form-item label="Lot 选择" class="form-item--required">
                <!-- 搜索框 -->
                <el-input v-model="lotSearch" placeholder="搜索 Lot" prefix-icon="el-icon-search" class="search-input" />

                <!-- 已选标签区 -->
                <div class="selected-tags">
                  <el-tag v-for="lot in formData.lots" :key="lot" closable size="medium" @close="removeLot(lot)"
                    class="tag-item">
                    {{ lot }}
                  </el-tag>
                </div>

                <!-- Lot 列表 -->
                <div class="lot-list">
                  <el-scrollbar class="list-scrollbar">
                    <div v-for="lot in filteredLots" :key="lot" class="list-item" @click="selectLot(lot)">
                      <el-checkbox :label="lot" :checked="formData.lots.includes(lot)" class="full-width" />
                    </div>
                  </el-scrollbar>
                </div>
              </el-form-item>
            </el-form>
          </div>
        </el-col>
      </el-row>
    </div>

    <!-- 对话框底部 -->
    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleClose">取消</el-button>
        <el-button type="primary" @click="handleConfirm">
          确定
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script>
import { GetLotListIdle} from "@/api/lot"
import { GetIdle} from "@/api/carrier"

export default {
  props: {
    visible: {
      type: Boolean,
      default: false
    },
    isEdit: {
      type: Boolean,
      default: false
    },
    form: {
      type: Object,
      default: () => ({})
    }
  },
  data() {
    return {
      dialogVisible: this.visible,
      formData: { ...this.form },
      lotSearch: '',
      devices: ['AHEG07'],
      carriers: [],
      lots: []
    }
  },
  computed: {
    filteredLots() {
      const searchTerm = this.lotSearch.toLowerCase()
      return this.lots.filter(lot =>
        lot.toLowerCase().includes(searchTerm) &&
        !this.formData.lots.includes(lot)
      )  // 这里缺少了闭合的括号
    },
  },
  created () {
     this.getLotList()
    this.getCarrierList()
  },
  methods: {
    getLotList(){
      GetLotListIdle('').then(res=>{
            this.lots = res.data.list
          })
    },
    getCarrierList(){
      GetIdle('').then(res=>{
            this.carriers = res.data.list
          })
    },
    handleClose() {
      this.dialogVisible = false
    },
    // 选择 Lot
    selectLot(lot) {
      if (!this.formData.lots.includes(lot)) {
        this.formData.lots = [...this.formData.lots, lot]
      }
    },

    // 移除 Lot
    removeLot(lot) {
      this.formData.lots = this.formData.lots.filter(item => item !== lot)
    },
   
    handleConfirm() {
        this.$emit('submit', { ...this.formData })
          this.dialogVisible = false
    },
    // 表单验证
    validateForm() {
      return (
        true
      )
    },
  },
  watch: {
    visible(newVal) {
      this.dialogVisible = newVal
      if (newVal) {
        this.formData = { ...this.form }
      }
    },
    dialogVisible(newVal) {
      this.$emit('update:visible', newVal)
    },
  }
}
</script>

<style lang="scss" scoped>
.dialog-container {
  padding: 0 20px;

  .form-section,
  .lot-section {
    height: 480px;
    padding: 0 8px;
  }

  // 表单项样式
  .form-item--required {
    ::v-deep .el-form-item__label::before {
      content: '*';
      color: #f56c6c;
      margin-right: 4px;
    }
  }

  // 搜索框
  .search-input {
    margin-bottom: 12px;
  }

  // 已选标签区
  .selected-tags {
    min-height: 40px;
    max-height: 80px;
    padding: 8px;
    margin-bottom: 12px;
    border: 1px solid #ebeef5;
    border-radius: 4px;
    overflow-y: auto;

    .tag-item {
      margin: 2px 4px 2px 0;
    }
  }

  // 列表容器
  .lot-list {
    text-align: left;
    border: 1px solid #ebeef5;
    border-radius: 4px;

    // 滚动条容器
    .list-scrollbar {
      height: 300px;
    }

    // 列表项
    .list-item {
      padding: 2px;
      cursor: pointer;
      transition: background-color 0.3s;

      &:hover {
        background-color: #f5f7fa;
      }

      ::v-deep .el-checkbox__label {
        width: 100%;
        padding-left: 10px;
      }
    }
  }

  // 底部按钮
  .dialog-footer {
    padding: 16px 20px 0;
    text-align: right;
  }
}

// 通用样式
.full-width {
  width: 100%;
}
</style>