// 第1步：导入组件
import Tree from './Tree'
const install = (Vue, _config) => {
  if (install.installed) {
    return
  }
  // 第2步：注册组件
  Vue.component(Tree.name, Tree)
  install.installed = true
}
// 第3步：导出组件
export default {
  install
}
