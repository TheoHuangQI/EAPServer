<template>
  <el-dialog
    :title="isEdit ? '编辑Lot信息' : 'LOT信息录入'"
    :visible.sync="dialogVisible"
    width="800px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form 
      ref="form"
      :model="formData" 
      :rules="rules"
      label-width="100px"
    >
      <!-- 批次号 -->
      <el-form-item label="批次号：" prop="lotId">
        <el-input 
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
              :label="slot.value"
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

<script>
import { Alert } from 'element-ui'
import { GetPPIDList} from "@/api/lot"
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
    const validateQty = (rule, value, callback) => {
      if (value === '') return callback(new Error('请输入数量'))
      if (typeof value !== 'number') return callback(new Error('必须为数字'))
      if (value < 0 || value > 25) {
        callback(new Error('数量必须在0-25之间'))
      } else {
        callback()
      }
    }
    return {
      dialogVisible: this.visible,
      formData: { ...this.form },
      ppidOptions: ['PPID-A001', 'PPID-B002', 'PPID-C003', 'PPID-D004'],
      rules: {
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
    }
  },
  computed: {
    slotOptions() {
      return Array.from({ length: 25 }, (_, i) => ({
        value: `${this.formData.lotId || ''}.${String(i + 1).padStart(2, '0')}`,
        label: `${this.formData.lotId || ''}.${String(i + 1).padStart(2, '0')}`
      }))
    },
    isAllSelected() {
      return this.formData.slots.length === 25
    },
    selectedCount() {
      return this.formData.slots.length
    },
  },
  methods: {
    getppList(){
      GetPPIDList(this.formData.lotId).then(res=>{
            this.ppidOptions = res.data.list
          })
    },
    handleClose() {
      this.dialogVisible = false
    },
    toggleSelectAll() {
      if (this.isAllSelected) {
        this.formData.slots = []
      } else {
        this.formData.slots = this.slotOptions.map(slot => slot.value)
      }
    },
    handleConfirm() {
      this.$refs.form.validate(valid => {
        if (valid) {
          this.$emit('submit', { ...this.formData })
          this.dialogVisible = false
        }
      })
    }
  },
  watch: {
    visible(newVal) {
      this.dialogVisible = newVal
      if (newVal) {
        this.formData = { ...this.form }
        this.getppList()
      }
    },
    dialogVisible(newVal) {
      this.$emit('update:visible', newVal)
    },
  }
}
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

.slot-item >>> .el-checkbox__label {
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