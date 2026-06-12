import { createApp } from 'vue'
import App from './App.vue'
import ElementPlus from 'element-plus'; // 引入 Element Plus
import zhCn from 'element-plus/dist/locale/zh-cn.mjs'
import 'element-plus/dist/index.css'; // 引入 Element Plus 的样式
import router from './router'; // 引入路由配置
import '@fortawesome/fontawesome-free/css/all.css';
import store from './store'
import Tree from '@/components'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'

const app = createApp(App);

for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}
// 使用 Element Plus
app.use(ElementPlus, { locale: zhCn });
app.use(router);
app.use(store);
app.use(Tree);

app.mount('#app');
