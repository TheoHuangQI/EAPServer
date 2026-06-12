<template>
  <div class="app-container" style="padding:10px;">
    <div class="main">
      <div class="main-item left">
        <TempTree :source-data="TestTempList" :source-type="2" @tree-click="handleNodeClick" @add-node="addNode"/>
      </div>
      <div class="main-item right">
        <!-- 新增顶部操作栏 -->
        <div class="top-action-bar">
          <el-label v-text="TestTempName" style="width: auto; margin-right: 50px;font-weight: bold; margin-top: 10px;" />
          <el-button type="primary"  @click="handleSave"  v-bind:style="{ display: TransferVisible ? 'block' : 'none' }">
            Save
          </el-button>
        </div>
        <Transfer1 :selectedkeys="selectedItems"  :source-data="TestItemList"  :title="TestItemTitle"  v-bind:style="{ display: TransferVisible ? 'block' : 'none' }"
          @update:selecteditems="handleSelectItemUpdate"></Transfer1>

        <Transfer2 :selectedkeys="selectedBatchs"  :source-data="TestBatchList" :title="TestBatchTitle"  v-bind:style="{ display: TransferVisible ? 'block' : 'none' }"
          @update:selecteditems="handleSelectBatchUpdate"></Transfer2>
      </div>
    </div>
  </div>
</template>
<script>
import Transfer1 from './TestItemTransfer.vue'
import Transfer2 from './TestBatchTransfer.vue'
import { GetAllBatch, GetTestItemList, saveTestTemplate, GetTestTemplateList,addTestTemplate} from "@/api/Test"
import TempTree from './TempTree.vue'
export default {
  components: { Transfer1,Transfer2,TempTree},
  data() {
    return {
      TransferVisible:false,
      TestTempName: '',
      TestTempId: null,
      treeData: [],
      fileList: [],
      defaultProps: {
        children: 'children',
        label: 'label'
      },
      TestItemTitle:'单测试项选择',
      TestBatchTitle:'复合测试项选择',
      TestTempList: [],
      TestItemList: [],
      TestBatchList: [],
      selectedItems: [],
      selectedBatchs: [],
      selectedItems1: [],
      selectedBatchs1: [],
    }
  },
  created() {
    this.GetAllTestTemplate()
    this.GetAllTestBatch()
    this.GetAllTestItem()
  },
  methods: {
    handleSelectItemUpdate(newSelectedItems) {
      this.selectedItems1 = newSelectedItems;  // 更新父组件中的 selectedKeys
    },
    handleSelectBatchUpdate(newSelectedItems) {
      this.selectedBatchs1 = newSelectedItems;  // 更新父组件中的 selectedKeys
    },
    handleNodeClick(data) {
      this.TestTempName = data.label
      this.TestTempId = data.id
      this.TransferVisible = true
      const selectedTemplate = this.TestTempList.find(a => a.testTemplateId == data.id)
      if (selectedTemplate) {
        this.selectedItems = selectedTemplate.testItems || []
        this.selectedBatchs = selectedTemplate.testBatchs || []
      } else {
        // 如果是新节点或者找不到，则设置为空数组
        this.selectedItems = []
        this.selectedBatchs = []
      }
    },
    addNode(data){
      const formdata = {
        testTemplateName: data.testTemplateName,
      }
      addTestTemplate(formdata).then(res => {
        if (res.return == "OK") {
          this.$message.success('保存成功')
          this.GetAllTestTemplate()
        }
        else {
          this.$message.error(res.message)
          this.GetAllTestTemplate()
        }
      })
    },
    // 新增保存方法
    handleSave() {
      const formdata = {
        TestTemplateId: this.TestTempId,
        TestTemplateName: this.TestTempName,
        TestItems: this.selectedItems1,
        TestBatchs:this.selectedBatchs1
      }
      saveTestTemplate(formdata).then(res => {
        if (res.return == "OK") {
          this.$message.success('保存成功')
          this.GetAllTestTemplate()
        }
        else {
          this.$message.error(res.message)
          this.GetAllTestTemplate()
        }
      })
    },
    GetAllTestTemplate() {
      this.loading = true
      try {
        GetTestTemplateList().then(res => {
          this.TestTempList = res.data.list
        })
      } finally {
        this.loading = false
      }
    },
    GetAllTestBatch() {
      this.loading = true
      try {
        GetAllBatch().then(res => {
          this.TestBatchList = res.data.list
        })
      } finally {
        this.loading = false
      }
    },
    GetAllTestItem() {
      this.loading = true
      try {
        GetTestItemList("").then(res => {
          this.TestItemList = res.data.list
        })
      } finally {
        this.loading = false
      }
    }
  }
}
</script>
<style  lang="scss" scoped>
.app-container {
  left: 0;
  top: 0;
  right: 0;
  bottom: 0;
  width: 100%;
  height: 100%;
  background-color: white;
  position: fixed;
  margin-left: 256px;
  margin-top: 64px;

  .main {
    display: flex;
    width: 100%;
    height: 100%;
    min-height: 0;

    /* 修复 Chrome flex 溢出问题 */
    .main-item {
      display: flex;
      border: 1px solid #e3e3e3;
      border-radius: 5px;
      padding: 10px;
      overflow-x: hidden;
      overflow-y: auto;
    }

    .left {
      width: 240px;
      overflow-y: auto;
    }

    .right {
      background: #f5f7fa;
      margin-left: 10px;
      flex: 1;
      display: flex;
      /* 启用 Flexbox */
      flex-direction: column;
      /* 子元素垂直排列 */
      padding: 0px 10px; // 添加内边距
    }
  }
}

.mytree :deep() {
  .el-tree-node.is-current>.el-tree-node__content {
    background-color: #c2d6ea !important;
  }

  .el-tree>.el-tree-node:after {
    border-top: none;
  }

  .el-tree-node {
    position: relative;
    padding-left: 1px;
  }

  .treetitle {
    position: relative;
    padding-left: 1px;
  }

  .el-tree-node__children {
    padding-left: 16px;
  }

  .el-tree-node :last-child:before {
    height: 38px;
  }

  .el-tree>.el-tree-node:before {
    border-left: none;
  }

  .el-tree>.el-tree-node:after {
    border-top: none;
  }

  .el-tree-node:before {
    content: "";
    left: -4px;
    position: absolute;
    right: auto;
    border-width: 1px;
  }

  .el-tree-node:after {
    content: "";
    left: -4px;
    position: absolute;
    right: auto;
    border-width: 1px;
  }

  .el-tree-node:before {
    border-left: 1px dashed #4386c6;
    bottom: 0px;
    height: 100%;
    top: -26px;
    width: 1px;
  }

  .el-tree-node:after {
    border-top: 1px dashed #4386c6;
    height: 20px;
    top: 12px;
    width: 24px;
  }
}

.top-action-bar {
  padding: 5px 0PX;
  border-radius: 4px;
  display: flex;
  justify-content: flex-start;
  /* 按钮靠右 */
}
</style>