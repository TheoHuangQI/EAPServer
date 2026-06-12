<template>
  <div>
    <el-input v-model="filterText" placeholder="请输入搜索内容" clearable @input="filterData" />
    <div class="mytree">
      <el-tree ref="tree" :indent="0" :data="treeData" :props="defaultProps" :render-content="renderTreeContent"
        node-key="id" class="filter-tree" default-expand-all highlight-current @node-click="treeNodeClick" />
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
      default: () => { }
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
        children: 'children'
      },
      treeData: []
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
    convertToTreeData (dataList, id) {
      return dataList.reduce((res, current) => {
        if (current['parentId'] === id) {
          current.label = current.name
          return res.concat(current)
        }
        return res
      }, [])
    },
    // 自定义渲染树节点内容
    renderTreeContent(h, { node, data, store }) {
      return (
        <span class='custom-tree-node'>
          <i class='el-icon-set-up'></i>
          <span style='margin-left:5px;'>{node.label}</span>
        </span>
      )
    },
    // 树节点点击
    treeNodeClick(data) {
      this.$emit('tree-click', data)
      console.log('data', data)
    },
    // 将数据转为树形数据
    transDataToTreeData(dataList) {
      dataList=this.SourceData
      console.log('这是transDataToTreeData', dataList)
      // 第1步：将原始数据克隆一份
      const cloneDataList = cloneDeep(dataList)
      // 第2步：循环遍历判断是否存在parentId属性，如果不存在则加上
      cloneDataList.forEach(item => {
        if (!item.parentId) {
          item.parentId = '0'
        }
      })
      // 第3步：将克隆后的数据根据equipmentType进行分组
      const groupByDataList = groupBy(cloneDataList, 'type')
      const tempArr = []
      // 第4步：循环遍历分组后的数据，将其处理成tree控件所需要的格式
      Object.keys(groupByDataList).map(key => {
        tempArr.push({
          id: key,
          label: key,
          children: this.convertToTreeData(groupByDataList[key], '0')
        })
      })
      this.treeData = tempArr
    },
    // 将类型数据转为树形数据
    transTypeDataToTreeData(dataList) {
      console.log('这是transTypeDataToTreeData', dataList)
      const treeList = dataList.map((item, index) => {
        return {
          id: item.testBatchId,
          label: item.testBatchName,
        }
      })
      this.treeData = treeList
    },
    filterData(val) {
      const reg = new RegExp(val, 'i')
      let filterDataList = []
      if (this.sourceType === 1) {
        if (!val) {

          filterDataList = this.SourceData
        } else {
          filterDataList = this.SourceData.filter(item => {
            return reg.test(item.name) || reg.test(item.name)
          }
          )
        }
        this.transDataToTreeData(filterDataList)
      } else {
        if (!val) {
          filterDataList = this.SourceData
        } else {
          filterDataList = this.SourceData.filter(item => reg.test(item))
        }
        this.transTypeDataToTreeData(filterDataList)
      }
    }
  }
}
</script>
<style lang="scss" scoped>
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
</style>