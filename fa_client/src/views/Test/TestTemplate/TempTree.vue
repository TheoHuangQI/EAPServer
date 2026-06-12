<template>
  <div>
    <div style="display: flex; margin-bottom: 10px;">
      <el-input v-model="filterText" placeholder="请输入搜索内容" clearable @input="filterData"
        style="flex: 1; margin-right: 10px;" />
      <el-button type="primary" icon="el-icon-plus" @click="handleAddNode">新增</el-button>
    </div>
    <div class="mytree">
      <el-tree ref="tree" :indent="0" :data="treeData" :props="defaultProps"
        node-key="id" class="filter-tree" default-expand-all highlight-current @node-click="treeNodeClick">
        <template #default="{ node, data }">
          <span class="custom-tree-node">
            <template v-if="data.isEditing">
              <el-input
                :model-value="data.label"
                @update:model-value="value => data.label = value"
                @keyup.enter="handleSaveNode(data)"
                style="width: 120px; margin-right: 8px;"
              />
            </template>
            <template v-else>
              <i class="el-icon-set-up"></i>
              <span style="margin-left: 5px; margin-right: 8px;">{{ node.label }}</span>
            </template>
          </span>
        </template>
      </el-tree>
    </div>
  </div>
</template>
<script>
import { cloneDeep, groupBy } from 'lodash'
export default {
  name: 'Tree',
  props: {
    //源数据
    SourceData: {
      type: Array,
      default: () => []
    },
    // 数据源类型
    sourceType: {
      type: Number,
      default: 1
    }
  },
  data() {
    return {
      filterText: null,
      defaultProps: {
        children: 'children',
        label: 'label'
      },
      treeData: [],
      localNodes: [] // 存储本地新增的临时节点
    }
  },
  watch: {
    SourceData: {
      handler: function (val) {
        this.filterData(null)
      },
      immediate: true
    }
  },
  methods: {
    // 新增节点处理
    handleAddNode() {
      const newNode = {
        id: `temp_${Date.now()}`, // 临时ID
        label: '',
        isEditing: true,
        testItems:[],
        testBatchs:[],
        isTemp: true,
        parentId: '0',
        type: 'new_type' // 根据实际情况设置默认类型
      }

      if (this.sourceType === 2) {
        // 类型2的数据结构处理
        newNode.testTemplateId = `temp_${Date.now()}`
        newNode.testTemplateName = ''
      }

      this.localNodes.push(newNode)
      this.filterData(this.filterText)
    },

    // 保存节点
    handleSaveNode(node) {
      node.isEditing = false
      // 构造新的节点数据
      const newNodeData = this.sourceType === 1 ? {
          id: node.id,
          name: node.label,
          parentId: node.parentId,
          type: node.type
        } : {
          testTemplateId: node.testTemplateId,
          testTemplateName: node.label,
          isEditing:false
        }

        // 触发新增事件
        this.$emit('add-node', newNodeData)
        // 移除临时节点
        this.localNodes = this.localNodes.filter(n => n.id !== node.id)
    },

    // 删除节点
    handleDeleteNode(node) {
      if (node.isTemp) {
        // 删除临时节点
        this.localNodes = this.localNodes.filter(n => n.id !== node.id)
      } else {
        // 触发删除事件
        this.$emit('delete-node', node)
      }
      this.filterData(this.filterText)
    },

    convertToTreeData(dataList, id) {
      return dataList.reduce((res, current) => {
        if (current['parentId'] === id) {
          current.label = current.name
          return res.concat(current)
        }
        return res
      }, [])
    },
    // 树节点点击
    treeNodeClick(data) {
      this.$emit('tree-click', data)
      console.log('data', data)
    },

    // 修改后的树结构转换方法
    transDataToTreeData(dataList) {
      const cloneDataList = cloneDeep(dataList)
      cloneDataList.forEach(item => {
        if (!item.parentId) item.parentId = '0'
        item.label = item.name || item.label
      })

      const grouped = groupBy(cloneDataList, 'type')
      this.treeData = Object.keys(grouped).map(type => ({
        id: type,
        label: type,
        children: this.convertToTreeData(grouped[type], '0')
      }))
    },

    // 将类型数据转为树形数据
    transTypeDataToTreeData(dataList) {
      console.log('这是transTypeDataToTreeData', dataList)
      const treeList = dataList.map((item, index) => {
        return {
          id: item.testTemplateId,
          label: item.testTemplateName,
          isEditing:item.isEditing
        }
      })
      this.treeData = treeList
    },

    filterData(val) {
      const reg = new RegExp(val, 'i')
      const sourceData = Array.isArray(this.SourceData) ? this.SourceData : []
      let combinedData = [...this.localNodes, ...sourceData]
      if (val) {
        combinedData = combinedData.filter(item =>
          this.sourceType === 1
            ? reg.test(item.name)
            : reg.test(item.testTemplateName)
        )
      }
      if (this.sourceType === 1) {
        this.transDataToTreeData(combinedData)
      } else {
        this.transTypeDataToTreeData(combinedData)
      }
    }
  }
}
</script>
<style lang="scss" scoped>
.mytree :deep() {
  .el-tree-node__content {
    height: 32px;
    .custom-tree-node {
      display: flex;
      align-items: center;
      width: 100%;
    }
    .el-input__inner {
      height: 28px;
    }
  }
  .el-button {
    padding: 5px;
    margin-left: 8px;
  }

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
</style>