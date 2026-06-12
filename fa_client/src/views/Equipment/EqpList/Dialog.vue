<template>
  <el-dialog :title="isEdit ? '编辑设备信息' : '添加设备'" :model-value="visible" width="600px" :close-on-click-modal="false"
    @close="handleClose">
    <el-form ref="formRef" :model="formData" :rules="rules" label-width="100px">
      <!-- 设备号 -->
      <el-form-item label="设备号" prop="eqpId">
        <el-input :disabled="isEdit" v-model="formData.eqpId" placeholder="请输入设备号" clearable style="width: 100%" />
      </el-form-item>

      <!-- 设备类型 -->
      <el-form-item label="设备类型" prop="eqpType">
        <el-input v-model="formData.eqpType" placeholder="请输入设备类型" clearable style="width: 100%" />
      </el-form-item>

      <!-- 载具选择 -->
      <el-form-item label="启用" prop="enable">
        <el-select v-model="formData.enable" placeholder="是否启用设备" clearable style="width: 100%">
          <el-option v-for="option in enableOption" :key="option.index" :label="option.value" :value="option.index" />
        </el-select>
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
import { ref, watch } from 'vue'

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
const formData = ref({})
const enableOption = ref([{ index: 0, value: "Disable" }, { index: 1, value: "Enable" }])

const rules = {
  eqpId: [
    { required: true, message: '请输入批次号', trigger: 'blur' }
  ],
  eqpType: [
    { required: true, message: '请输入设备类型', trigger: 'blur' }
  ],
  enable: [
    { required: true, message: '请选择是否启用设备', trigger: 'blur' }
  ],
}

const handleClose = () => {
  emit('update:visible', false)
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
  }
})
</script>
<style scoped>
.dialog-footer {
  margin-top: 20px;
  text-align: right;
}
</style>