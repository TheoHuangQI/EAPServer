import Vue from 'vue'
import App from './App.vue'
import ElementUI from 'element-ui'; // 引入 Element UI
import 'element-ui/lib/theme-chalk/index.css'; // 引入 Element UI 的样式
import router from './router'; // 引入路由配置
import '@fortawesome/fontawesome-free/css/all.css';
import store from './store'  
import * as echarts from 'echarts';

Vue.config.productionTip = false
// 使用 Element UI
Vue.use(ElementUI);


new Vue({
  router,// 注入路由
  store,
  echarts,
  render: h => h(App),
}).$mount('#app')
