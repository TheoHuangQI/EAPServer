<template>
  <!-- 设备选择对话框 -->
  <el-dialog 
  :title="isEdit ? 'Batch修改' : '新增Batch'"
  :model-value="visible"
  width="960px" 
  :close-on-click-modal="false"
  @close="handleClose">
    <!-- 主内容区 -->
    <div class="dialog-container">
      <el-row :gutter="24">
        <!-- 左侧表单列 -->
        <el-col :span="12">
          <div class="form-section">
            <el-form label-position="left" label-width="100px">
              <!-- 设备选择 -->
              <el-form-item label="设备选择" class="form-item--required">
                <el-select v-model="formData.eqpId" placeholder="请选择设备" clearable class="full-width" :loading="deviceLoading">
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

              <!-- Port选择 -->
              <el-form-item label="Port" class="form-item--required">
                <el-input v-model="formData.portId" placeholder="请输入Port ID" />
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

<script setup>
import { ref, watch, computed } from 'vue'
import { GetLotListIdle } from "@/api/lot"
import { GetIdle } from "@/api/carrier"
import { GetList as GetEquipmentList } from "@/api/equipment"

const props = defineProps({
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
})

const emit = defineEmits(['update:visible', 'submit'])

const formData = ref({ lots: [] })
const lotSearch = ref('')
const devices = ref([])
const deviceLoading = ref(false)
const carriers = ref([])
const lots = ref([])

const filteredLots = computed(() => {
  const searchTerm = lotSearch.value.toLowerCase()
  return lots.value.filter(lot =>
    lot.toLowerCase().includes(searchTerm) &&
    !formData.value.lots.includes(lot)
  )
})

const getLotList = () => {
  GetLotListIdle('').then(res => {
    lots.value = res.data.list
  })
}

const isConnected = (status) => Number(status) === 1

const getDeviceList = () => {
  deviceLoading.value = true
  GetEquipmentList({}).then(res => {
    const list = res.data.list || []
    devices.value = list
      .filter(device => isConnected(device.eqpStatus) && isConnected(device.eapStatus))
      .map(device => device.eqpId)
      .filter(Boolean)
  }).finally(() => {
    deviceLoading.value = false
  })
}

const getCarrierList = (eqpId) => {
  if (!eqpId) {
    carriers.value = []
    return
  }
  GetIdle(eqpId).then(res => {
    carriers.value = res.data.list
  })
}

const handleClose = () => {
  emit('update:visible', false)
}

const selectLot = (lot) => {
  if (!formData.value.lots.includes(lot)) {
    formData.value.lots = [...formData.value.lots, lot]
  }
}

const removeLot = (lot) => {
  formData.value.lots = formData.value.lots.filter(item => item !== lot)
}

const handleConfirm = () => {
  emit('submit', { ...formData.value })
  handleClose()
}

watch(() => props.visible, (newVal) => {
  if (newVal) {
    formData.value = { ...props.form }
    // 确保 lots 是一个数组，即使后端没返回
    if (!formData.value.lots) {
      formData.value.lots = []
    }
    
    getDeviceList()
    // 自动填充操作员
    if (!props.isEdit) {
      try {
        const userInfo = JSON.parse(localStorage.getItem("UserInfo"))
        if (userInfo && userInfo.user && userInfo.user.workNo) {
          formData.value.operator = userInfo.user.workNo
        }
      } catch (e) {
        console.error("Failed to get user info from localStorage for auto-fill", e)
      }
      // 如果是新增，确保 lots 是一个空数组
      if (!formData.value.lots) {
        formData.value.lots = []
      }
    }
    // 对话框打开时，如果已有设备ID（编辑模式），则加载对应的载具列表
    if (formData.value.eqpId) {
      getCarrierList(formData.value.eqpId)
    }
    // 如果是编辑模式且有载具，则加载Lot列表
    if (props.isEdit && formData.value.carrierId) {
      getLotList()
    }
  }
})

// 监听设备选择的变化
watch(() => formData.value.eqpId, (newEqpId, oldEqpId) => {
  // 当设备改变时，清空已选择的载具并重新加载列表
  if (newEqpId !== oldEqpId) {
    formData.value.carrierId = ''
    getCarrierList(newEqpId)
  }
})

// 监听载具选择的变化
watch(() => formData.value.carrierId, (newCarrierId) => {
  // 当选择了载具后，加载Lot列表
  if (newCarrierId) {
    getLotList()
  } else {
    // 如果清空了载具，不应清空已选的Lot，只清空待选列表
    lots.value = []
  }
})
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
    :deep() .el-form-item__label::before {
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

      :deep(.el-checkbox__label) {
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
