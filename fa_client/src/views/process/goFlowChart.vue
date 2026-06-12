<template>
  <el-row>
    <el-col id="parentDiv" :span="24" style="text-align: center;">
      <div id="mygoChart" style="width:100%;height:250px;" />
    </el-col>
    <el-col v-if="approver.length>0" :span="24" style="text-align: center;">
      <el-table
        :data="approver.filter(data => !search || data.username.toLowerCase().includes(search.toLowerCase())|| data.nickname.toLowerCase().includes(search.toLowerCase()))"
        size="small"
        border
        fit
        highlight-current-row>
        <el-table-column :label="用户名" prop="username" width="100" align="center"/>
        <el-table-column :label="名称" prop="nickname" width="80" align="center" />
        <el-table-column :label="邮箱" prop="mail" align="center"/>
        <el-table-column :label="分组" prop="roleIds" min-width="90" align="center">
          <template v-slot="curr">
            <div style="white-space:pre-wrap;">
              <el-tag v-for="(item,index) in curr.row.roleIds" :key="index" type="primary" size="mini" style="margin:5px;">{{ groupList.find(p=>p.roleId===item).roleName }}</el-tag>
            </div>
          </template>
        </el-table-column>
        <el-table-column
          align="right">
          <template #header="scope">
            <el-input
              v-model="search"
              :placeholder="请输入用户名"
              size="small"/>
          </template>
        </el-table-column>
      </el-table>
    </el-col>
  </el-row>
</template>
<script>
export default {
  name: 'FlowChart',
  props: {
    currFlow: {
      type: Object,
      default() {
        return {}
      }
    }
  },
  data: function() {
    return {
      loading: false,
      search: '',
      groupList: [],
      userList: [],
      flowList: [],
      approver: [],
      nodeDataArray: [],
      linkDataArray: [],
      getFlowStepInfo: { key: '' }
    }
  },
  mounted() {
    this.$nextTick().then(() => {
      this.lookHandler()
    })
  },
  methods: {
    
  }
}
</script>