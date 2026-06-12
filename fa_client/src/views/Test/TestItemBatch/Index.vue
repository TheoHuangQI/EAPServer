<template>
  <div class="app-container" style="padding:10px;">
    <div class="main">
      <div class="main-item left">
        <tree :source-data="TestBatchList" :source-type="2" @tree-click="handleNodeClick"   @add-node="addNode"/>
      </div>
      <div class="main-item right">
        <!-- 新增顶部操作栏 -->
        <div class="top-action-bar">
          <el-label v-text="TestBatchName" style="width: auto; margin-right: 50px;font-weight: bold; margin-top: 10px;" />
          <el-button type="primary"  @click="handleSave"  v-bind:style="{ display: TransferVisible ? 'block' : 'none' }">
            Save
          </el-button>
        </div>
        <Transfer :selectedkeys="selectedItems"  :source-data="TestItemList"   v-bind:style="{ display: TransferVisible ? 'block' : 'none' }"
          @update:selecteditems="handleSelectedKeysUpdate"  ></Transfer>
      </div>
    </div>
  </div>
</template>
<script>
import Transfer from '@/views/Test/TestItemBatch/Transfer.vue'
import { GetTestBatchList, GetTestItemList, saveTestBatch, addTestBatch } from "@/api/Test"
export default {
  components: { Transfer },
  data() {
    return {
      TransferVisible:false,
      TestBatchName: '',
      TestBatchId: null,
      treeData: [],
      fileList: [],
      defaultProps: {
        children: 'children',
        label: 'label'
      },
      TestBatchList: [],
      TestItemList: [],
      selectedItems: [],
      selectedItems1: [],
    }
  },
  created() {
    this.GetAllTestBatch()
    this.GetAllTestItem()
  },
  methods: {
    handleSelectedKeysUpdate(newSelectedItems) {
      this.selectedItems1 = newSelectedItems;  // 更新父组件中的 selectedKeys
    },
    handleNodeClick(data) {
      this.TestBatchName = data.label
      this.TestBatchId = data.id
      this.TransferVisible = true
      const selectedBatch = this.TestBatchList.find(a => a.testBatchId == data.id)
      if (selectedBatch) {
        this.selectedItems = selectedBatch.testItems || []
      } else {
        this.selectedItems = []
      }
    },
    addNode(data){
      const formdata = {
        TestBatchName: data.testBatchName,
      }
      addTestBatch(formdata).then(res => {
        if (res.return == "OK") {
          this.$message.success('保存成功')
          this.GetAllTestBatch()
        }
        else {
          this.$message.error(res.message)
          this.GetAllTestBatch()
        }
      })
    },
    // 新增保存方法
    handleSave() {
      // 在发送请求前，增加防御性检查
      if (!this.TestBatchId || typeof this.TestBatchId !== 'number' || this.TestBatchId <= 0) {
        this.$message.error('请先从左侧树中选择一个有效的复合测试项');
        return;
      }

      try {
        const itemsToSend = this.selectedItems1.map(item => {
          if (!item) {
            console.warn('selectedItems1 中发现一个空的 item');
            return null;
          }
          // 根据后端代码，循环中只使用了 testItem.Id 和 testItem.MessageName
          // 因此只发送这两个转换后的字段
          return {
            Id: item.id,
            MessageName: item.messageName,
          };
        }).filter(item => item !== null);

        const formdata = {
          TestBatchId: this.TestBatchId,
          TestBatchName: this.TestBatchName,
          TestItems: itemsToSend,
        }

        console.log('即将发送到后端的数据:', JSON.stringify(formdata, null, 2));

        saveTestBatch(formdata).then(res => {
          if (res.return == "OK") {
            this.$message.success('保存成功')
            this.GetAllTestBatch()
          }
          else {
            this.$message.error(`保存失败: ${res.message}`)
            this.GetAllTestBatch()
          }
        }).catch(error => {
          console.error('请求失败，错误详情:', error);
          if (error.response) {
            console.error('后端返回的错误数据:', error.response.data);
            this.$message.error(`请求错误: ${error.response.data.title || error.message}`);
          } else {
            this.$message.error(`请求错误: ${error.message}`);
          }
        });
      } catch (e) {
        console.error('handleSave 内部发生同步错误:', e);
        this.$message.error('客户端代码发生错误，请查看控制台。');
      }
    },
    GetAllTestBatch() {
      this.loading = true
      try {
        GetTestBatchList().then(res => {
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
  position: fixed;
  margin-left: 256px;
  margin-top: 64px;
  background-color: white;

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
      padding: 10px; // 添加内边距
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
  margin-bottom: 5px;
  margin-left: 20px;
  padding: 5px 0;
  border-radius: 4px;
  display: flex;
  justify-content: flex-start;
  /* 按钮靠右 */
}
</style>