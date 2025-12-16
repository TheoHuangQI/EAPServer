<template>
    <div class="transfer-container">
        <div class="transfer-header">
            <el-label v-text="title" style="text-align: left; font-weight:bold;"></el-label>
            <div style="margin-left: auto;"></div> <!-- 用于 Flex 布局的占位符 -->
        </div>
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
                <div class="count-text">总数: {{ selectedOption.length }}</div>
            </template>
        </el-transfer>
    </div>
</template>
<script>
import { text } from 'd3';

export default {
    props: {
        title: {
            type: String,
            default: () => ''
        },
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
    data() {
        return {
            allOptions: [],
            selectedOptions: [],
            leftSelected: [],
            rightSelected: [],
        };
    },
    watch: {
        SourceData(newVal) {
            this.allOptions = [];
            newVal.forEach(element => {
                this.allOptions.push({
                    key: element.id,
                    label: element.testBatchName
                });
            });

        },
        selectedkeys(newVal) {
            this.selectedOptions = [];
            newVal.forEach(ele => {
                this.selectedOptions.push(ele.id);
            })
        },
        selectedOptions(newVal) {
            //debugger
            // 保存选择的顺序
            const selectedItems = []
            newVal.forEach(key => {
                selectedItems.push(this.SourceData.find(a => a.id == key))
            })
            this.$emit('update:selecteditems', selectedItems)
        },
    },
    computed: {
        // 可用选项（左侧）
        availableOptions() {
            return this.allOptions.filter(
                item => !this.selectedOptions.includes(item.key)
            )
        },
        // 已选选项（右侧）
        selectedOption() {
            // 按 selectedOptions 的顺序映射数据
            return this.selectedOptions.map(key =>
                this.allOptions.find(item => item.key === key)
            ).filter(item => item !== undefined); // 过滤无效项
        },
        // 动态标题（带选中计数）
        transferTitles() {
            return [
                `可选项 (${this.leftSelected.length}/${this.availableOptions.length})`,
                `已选项 (${this.rightSelected.length}/${this.selectedOption.length})`
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
    padding: 5px;
    background: #f5f7fa;
}

/* 使用 Flexbox 布局 */
.transfer-header {
    display: flex;
    justify-content: flex-start;
    align-items: center;
    margin-bottom: 10px;
    margin-top: 10px;
}


/* 自定义穿梭框样式 */
::v-deep .el-transfer {
    display: flex;
    justify-content: flex-start;
    text-align: left;
}

::v-deep .el-transfer-panel {
    width: 35%;
    height: 350px;
    border-radius: 4px;
    box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

/* 标题容器样式 */
::v-deep .el-transfer-panel__header {
    display: flex !important;
    align-items: center !important;
    justify-content: space-between !important;
    height: 40px !important;
    padding: 0 16px !important;
    background: #f5f7fa !important;
    border-radius: 4px 4px 0 0 !important;
}

/* 主标题样式 */
::v-deep .el-transfer-panel__header .el-checkbox {
    height: 100% !important;
    display: flex !important;
    align-items: center !important;
}

::v-deep .el-transfer-panel__body {
    height: calc(100% - 40px);
}

::v-deep .el-transfer-panel__list {
    height: 100%;
    padding: 5px 0;
}

::v-deep .el-transfer-panel__item {
    display: flex !important;
    align-items: center !important;
    padding: 0px 12px !important;
    min-height: 36px !important;
    /* 固定最小高度 */
}

::v-deep .el-checkbox {
    display: flex !important;
    align-items: center !important;
    width: 100%;
    /* 撑满容器 */
}

::v-deep .el-checkbox__label {
    line-height: 1.5 !important;
    vertical-align: middle !important;
    font-size: 14px !important;
    padding-left: 8px !important;
    /* 调整间距 */
}

::v-deep .el-checkbox__input {
    vertical-align: middle !important;
    position: relative;
    top: -1px;
    flex-shrink: 0;
    /* 防止缩小 */
}


/* 调整选择框大小 */
::v-deep .el-checkbox__inner {
    width: 16px !important;
    height: 16px !important;
}

::v-deep .el-transfer-panel__item:hover {
    background-color: #f5f7fa;
}

::v-deep .el-checkbox__inner::after {
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
::v-deep .el-transfer__buttons {
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    padding: 0 20px;
    margin: 0 10px;
}

/* 移除按钮默认旋转 */
::v-deep .el-transfer__button .el-icon-arrow-right {
    transform: none;
}

/* 调整按钮文字间距 */
::v-deep .el-transfer__button span {
    margin-left: 5px;
}

/* 自定义滚动条 */
::v-deep .el-transfer-panel__list::-webkit-scrollbar {
    width: 6px;
}

::v-deep .el-transfer-panel__list::-webkit-scrollbar-thumb {
    background-color: #c0c4cc;
    border-radius: 3px;
}

::v-deep .el-transfer-panel__list::-webkit-scrollbar-track {
    background-color: #f5f7fa;
}
</style>