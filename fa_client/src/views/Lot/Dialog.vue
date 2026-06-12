<template>
  <el-dialog
    :title="isEdit ? '编辑Lot信息' : 'LOT信息录入'"
    :model-value="visible"
    width="800px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form 
      ref="formRef"
      :model="formData" 
      :rules="rules"
      label-width="100px"
    >
      <!-- 批次号 -->
      <el-form-item label="批次号：" prop="lotId">
        <el-input 
        :disabled="isEdit"
          v-model="formData.lotId"
          placeholder="请输入批次号"
          clearable
          style="width: 100%"
        />
      </el-form-item>

      <!-- 数量 -->
      <el-form-item label="数量：" prop="qty">
        <el-input 
          type="number"
          v-model.number="formData.qty"
          placeholder="请输入数量(0-25)"
          :min="0"
          :max="25"
          style="width: 100%"
          clearable
        />
      </el-form-item>

      <!-- PPID -->
      <el-form-item label="PPID：" prop="ppid">
        <el-select 
          v-model="formData.ppid" 
          placeholder="请选择PPID"
          clearable
          filterable  
          style="width: 100%"
        >
          <el-option
            v-for="ppid in ppidOptions"
            :key="ppid"
            :label="ppid"
            :value="ppid"
          />
        </el-select>
      </el-form-item>

      <!-- 插槽选择 -->
      <el-form-item label="插槽选择" prop="slots">
        <div class="slot-container">
          <div class="slot-header">
            <div class="slot-title">
              <span class="count-text">（已选{{ selectedCount }}项）</span>
            </div>
            <el-button 
              type="text" 
              @click="toggleSelectAll"
              style="padding: 0"
            >
              {{ isAllSelected ? '取消全选' : '全选' }}
            </el-button>
          </div>
          <el-checkbox-group 
            v-model="formData.slots"
            class="slots-grid"
          >
            <el-checkbox
              v-for="slot in slotOptions"
              :key="slot.value"
              :value="slot.value"
              class="slot-item"
            >
              <span class="slot-label">{{ slot.label }}</span>
            </el-checkbox>
          </el-checkbox-group>
        </div>
      </el-form-item>

      <!-- 操作按钮 -->
      <el-form-item class="dialog-footer">
        <el-button @click="handleClose">取消</el-button>
        <el-button type="primary" @click="handleConfirm">确 定</el-button>
      </el-form-item>
    </el-form>
  </el-dialog>
</template>

<script setup>
import { ref, watch, computed } from 'vue'
import { GetPPIDList } from "@/api/lot"

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

const formRef = ref(null)
const formData = ref({ ...props.form })
const ppidOptions = ref([])

const validateQty = (rule, value, callback) => {
  if (value === '') return callback(new Error('请输入数量'))
  if (typeof value !== 'number') return callback(new Error('必须为数字'))
  if (value < 0 || value > 25) {
    callback(new Error('数量必须在0-25之间'))
  } else {
    callback()
  }
}

const rules = {
  lotId: [
    { required: true, message: '请输入批次号', trigger: 'blur' }
  ],
  qty: [
    { validator: validateQty, trigger: 'blur', required: true }
  ],
  ppid: [
    { required: false, message: '请选择PPID', trigger: 'change' }
  ]
}

const slotOptions = computed(() => {
  return Array.from({ length: 25 }, (_, i) => ({
    value: `${formData.value.lotId || ''}.${String(i + 1).padStart(2, '0')}`,
    label: `${formData.value.lotId || ''}.${String(i + 1).padStart(2, '0')}`
  }))
})

const isAllSelected = computed(() => formData.value.slots.length === 25)
const selectedCount = computed(() => formData.value.slots.length)

const getppList = () => {
  GetPPIDList(formData.value.lotId).then(res => {
    ppidOptions.value = res.data.list
  })
}

const handleClose = () => {
  emit('update:visible', false)
}

const toggleSelectAll = () => {
  if (isAllSelected.value) {
    formData.value.slots = []
  } else {
    formData.value.slots = slotOptions.value.map(slot => slot.value)
  }
}

const handleConfirm = () => {
  formRef.value.validate(valid => {
    if (valid) {
      emit('submit', { ...formData.value })
      handleClose()
    }
  })
}

watch(() => props.visible, (newVal) => {
  if (newVal) {
    formData.value = { ...props.form }
    // 仅在“编辑”模式下才加载PPID列表
    if (props.isEdit) {
      getppList()
    } else {
      // 在“新增”模式下，清空旧的选项以防数据显示错乱
      ppidOptions.value = []
    }
  }
})
</script>

<style scoped>
.slot-container {
  width: 100%;
}

.slot-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
}

.slot-title {
  font-size: 14px;
  color: #606266;
}

.slots-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 12px;
}

.slot-item {
  margin: 0;
  display: flex;
  align-items: center;
}

.slot-item :deep(.el-checkbox__label) {
  padding-left: 5px;
}

.slot-label {
  font-size: 12px;
  transform: scale(0.9);
}

.dialog-footer {
  margin-top: 20px;
  text-align: right;
}
</style>