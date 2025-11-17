import Vue from 'vue';
import Router from 'vue-router';
import Login from '@/views/Login.vue';
import Lot from '@/views/Lot/Index.vue';
import Carrier from '@/views/Carrier/Index.vue';
import Batch from '@/views/Batch/Index.vue';
import layout from '@/views/layout/Layout.vue';
import Dashboard from '../views/Dashboard.vue';
import Flow from '../views/Flow/Index1.vue';
import EAP from '../views/Equipment/EAP.vue'
import EqpList from '../views/Equipment/EqpList/Index.vue'
import Exception from '../views/Equipment/Exception.vue'
import RunHistory from  '../views/Equipment/RunHistory.vue'
import DataRule from  '../views/DataCollection/DataRule/Index.vue'
import DataHistory from  '../views/DataCollection/DataHistory/Index.vue'
import TestItem from  '../views/Test/TestItem/Index.vue'
import TestItemBatch from  '../views/Test/TestItemBatch/Index.vue'
import TestTemplate from  '../views/Test/TestTemplate/Index.vue'
import TestCase from  '../views/Test/TestCase/Index.vue'



const originalPush = Router.prototype.push;
Router.prototype.push = function push(location) {
  return originalPush.call(this, location).catch(err => {
    if (err.name !== 'NavigationDuplicated') {
      throw err;
    }
  });
};

Vue.use(Router);
const router = new Router({
  routes: [
    {
      path: '/',
      redirect: '/Login'
    },
    {
      path: '/Login',
      name: 'Login',
      component: Login,
    },
    {
      path: '/Index',
      name: 'Index',
      component: layout,
      children:[
        {
          path: '/Dashboard',
          name: 'Dashboard',
          component: Dashboard
        },
        {
          path: '/EAP',
          name: 'EAP',
          component: EAP
        },
        {
          path: '/Exception',
          name: 'Exception',
          component: Exception
        },
        {
          path: '/RunHistory',
          name: 'RunHistory',
          component: RunHistory
        },
        {
          path: '/DataRule',
          name: 'DataRule',
          component: DataRule
        },
        {
          path: '/DataHistory',
          name: 'DataHistory',
          component: DataHistory
        },
        {
          path: '/EqpList',
          name: 'EqpList',
          component: EqpList
        },
        {
          path: '/Batch',
          name: 'Batch',
          component: Batch
        },
        {
          path: '/LOT',
          name: 'LOT',
          component: Lot
        },
        {
          path: '/Carrier',
          name: 'Carrier',
          component: Carrier
        },
        {
          path: '/Flow',
          name: 'Flow',
          component: Flow
        },
        {
          path: '/TestItem',
          name: 'TestItem',
          component: TestItem
        },
        {
          path: '/TestItemBatch',
          name: 'TestItemBatch',
          component: TestItemBatch
        },
        {
          path: '/TestTemplate',
          name: 'TestTemplate',
          component: TestTemplate
        },
        {
          path: '/TestCase',
          name: 'TestCase',
          component: TestCase
        },
        {
          path: '/404',
          component: resolve => require(['@/views/errorPage/404'], resolve), 
          hidden: true
        },
        {
          path: '/401',
          component: resolve => require(['@/views/errorPage/401'], resolve),
          hidden: true
        }
      ]
    },
    // 其他路由配置
  ]
});
// router.beforeEach((to, from, next) => {
//   // const isAuthenticated = localStorage.getItem('token');
  
//   // // 如果访问的是登录页且已登录，直接跳转首页
//   // if (to.path === '/login' && isAuthenticated) {
//   //   next('/');
//   //   return;
//   // }

//   // // 如果访问的是需要登录的页面且未登录，跳转登录页
//   // if (to.matched.some(record => record.meta.requiresAuth) && !isAuthenticated) {
//   //   next('/login');
//   //   return;
//   // }

//   next();
// });
export default router;
