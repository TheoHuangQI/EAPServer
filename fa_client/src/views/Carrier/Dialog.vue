<template>
  <el-dialog
    :title="props.isEdit ? '编辑载具信息' : '新增载具信息'"
    :model-value="props.visible"
    width="500px"
    :close-on-click-modal="false"
    @update:model-value="emit('update:visible', $event)"
    @closed="handleClosed"
  >
    <el-form ref="formRef" :model="formData" label-width="100px">
      <el-form-item 
        label="载具编号" 
        prop="carrierId"
        :rules="[{ required: true, message: '请输入载具编号', trigger: 'blur' }]"
      >
        <el-input :disabled="props.isEdit" v-model="formData.carrierId" placeholder="请输入载具编号" />
      </el-form-item>
      <el-form-item
        label="尺寸"
        prop="size"
        :rules="[{ required: true, message: '请输入尺寸', trigger: 'blur' }]"
      >
        <el-input v-model="formData.size" placeholder="请输入尺寸" />
      </el-form-item>
      <el-form-item
        v-if="props.isEdit"
        label="使用状态"
        prop="in_use"
        :rules="[{ required: true, message: '请选择使用状态', trigger: 'change' }]"
      >
        <el-select v-model="formData.in_use" placeholder="请选择状态" style="width: 100%">
          <el-option label="空闲" :value="1" />
          <el-option label="使用中" :value="2" />
        </el-select>
      </el-form-item>
    </el-form>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="emit('update:visible', false)">取 消</el-button>
        <el-button type="primary" @click="handleConfirm">确 定</el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup>
import { ref, watch } from 'vue';

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
});

const emit = defineEmits(['update:visible', 'submit']);

const formRef = ref(null);
const formData = ref({ ...props.form });

watch(() => props.visible, (newVal) => {
  if (newVal) {
    formData.value = { ...props.form };
  }
});

const handleConfirm = () => {
  formRef.value.validate(valid => {
    if (valid) {
      emit('submit', { ...formData.value });
      emit('update:visible', false);
    }
  });
};

const handleClosed = () => {
  formRef.value.resetFields();
};
</script>