<template>
    <div class="transfer-container">
        <el-transfer v-model="selectedOptions" :data="allOptions" :titles="transferTitles" :target-order="'push'" :button-texts="['移到左侧', '移到右侧']"
            :format="{
                noChecked: '${total}',
                hasChecked: '${checked}/${total}'
            }" filterable @left-check-change="handleLeftCheck" @right-check-change="handleRightCheck">
            <!-- 自定义列表项内容 -->
            <template #default="{ option }">
                <span>{{ option.label }}</span>
            </template>

            <!-- 自定义标题 -->
            <template #left-footer>
                <div class="count-text">总数: {{ availableOptions.length }}</div>
            </template>
            <template #right-footer>
                <div class="count-text">总数: {{ selectedOptions.length }}</div>
            </template>
        </el-transfer>
    </div>
</template>
  
<script>
export default {
    props: {
        //源数据
        SourceData: {
            type: Array,
            default: () => []
        },
        selectedkeys: {
            type: Array,
            default: () => []
        }
    },
    watch: {
        SourceData(newVal) {
            //debugger
            this.allOptions=[];
            newVal.forEach(element => {
                this.allOptions.push({
                    key: element.id,
                    label: element.messageName+ "   " + element.description
                });
            });
           
        },
        selectedkeys(newVal){
            //debugger
            this.selectedOptions=[];
            newVal.forEach(ele => {
                this.selectedOptions.push(ele.id);
            })
        },
        selectedOptions(newVal) {
            const selectedItems = []
            newVal.forEach(key => {
                selectedItems.push(this.SourceData.find(a => a.id == key))
            })
            this.$emit('update:selecteditems', selectedItems)
        },
    },
    data() {
        return {
            allOptions: [],
            selectedOptions: [],
            leftSelected: [],
            rightSelected: []
        };
    },
    computed: {
        // 可用选项（左侧）
        availableOptions() {
            return this.allOptions.filter(
                item => !this.selectedkeys.includes(item.key)
            )
        },
        // 已选选项（右侧）
        selectedOptions() {
            return this.allOptions.filter(
                item => this.selectedkeys.includes(item.key))
        },
        // 动态标题（带选中计数）
        transferTitles() {
            return [
                `可选项 (${this.leftSelected.length}/${this.availableOptions.length})`,
                `已选项 (${this.rightSelected.length}/${this.selectedOptions.length})`
            ]
        }
    },
    methods: {
        handleLeftCheck(selected) {
            this.leftSelected = selected
        },
        handleRightCheck(selected) {
            this.rightSelected = selected
        }
    }
};
</script>
  
<style scoped>
.transfer-container {
    padding: 20px;
    background: #f5f7fa;
    height: 100%;
}

/* 自定义穿梭框样式 */
:deep() .el-transfer {
    display: flex;
    justify-content: flex-start;
    text-align: left;
}

:deep(.el-transfer-panel) {
    width: 35%;
    height: 700px;
    border-radius: 4px;
    box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

/* 标题容器样式 */
:deep(.el-transfer-panel__header) {
    display: flex !important;
    align-items: center !important;
    justify-content: space-between !important;
    height: 40px !important;
    padding: 0 16px !important;
    background: #f5f7fa !important;
    border-radius: 4px 4px 0 0 !important;
}

/* 主标题样式 */
:deep(.el-transfer-panel__header .el-checkbox) {
    height: 100% !important;
    display: flex !important;
    align-items: center !important;
}

:deep(.el-transfer-panel__body) {
    height: calc(100% - 40px);
}

:deep(.el-transfer-panel__list) {
    height: 100%;
    padding: 10px 0;
}

:deep(.el-transfer-panel__item) {
    display: flex !important;
    align-items: center !important;
    padding: 2px 12px !important;
    min-height: 36px !important;
    /* 固定最小高度 */
}

:deep(.el-checkbox) {
    display: flex !important;
    align-items: center !important;
    width: 100%;
    /* 撑满容器 */
}

:deep(.el-checkbox__label) {
    line-height: 1.5 !important;
    vertical-align: middle !important;
    font-size: 14px !important;
    padding-left: 8px !important;
    /* 调整间距 */
}

:deep(.el-checkbox__input) {
    vertical-align: middle !important;
    position: relative;
    top: -1px;
    flex-shrink: 0;
    /* 防止缩小 */
}


/* 调整选择框大小 */
:deep(.el-checkbox__inner) {
    width: 16px !important;
    height: 16px !important;
}

:deep(.el-transfer-panel__item:hover) {
    background-color: #f5f7fa;
}

:deep(.el-checkbox__inner::after) {
    left: 5px !important;
    top: 2px !important;
}

.count-text {
    font-size: 12px;
    color: #909399;
    padding: 2px 12px;
    border-top: 1px solid #e4e7ed;
}

/* 按钮样式调整 */
:deep(.el-transfer__buttons) {
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    padding: 0 20px;
    margin: 0 10px;
}

/* 移除按钮默认旋转 */
:deep(.el-transfer__button .el-icon-arrow-right) {
    transform: none;
}

/* 调整按钮文字间距 */
:deep(.el-transfer__button span) {
    margin-left: 5px;
}

/* 自定义滚动条 */
:deep(.el-transfer-panel__list::-webkit-scrollbar) {
    width: 6px;
}

:deep(.el-transfer-panel__list::-webkit-scrollbar-thumb) {
    background-color: #c0c4cc;
    border-radius: 3px;
}

:deep(.el-transfer-panel__list::-webkit-scrollbar-track) {
    background-color: #f5f7fa;
}
</style>