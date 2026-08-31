import { createRouter, createWebHashHistory } from 'vue-router';

const Login = () => import('@/views/Login.vue');
const Lot = () => import('@/views/Lot/Index.vue');
const Carrier = () => import('@/views/Carrier/Index.vue');
const Batch = () => import('@/views/Batch/Index.vue');
const layout = () => import('@/views/layout/Layout.vue');
const Dashboard = () => import('@/views/Dashboard.vue');
const Flow = () => import('@/views/Flow/Index1.vue');
const EAP = () => import('@/views/Equipment/EAP.vue');
const EqpList = () => import('@/views/Equipment/EqpList/Index.vue');
const Exception = () => import('@/views/Equipment/Exception.vue');
const RunHistory = () => import('@/views/Equipment/RunHistory.vue');
const DataRule = () => import('@/views/DataCollection/DataRule/Index.vue');
const DataHistory = () => import('@/views/DataCollection/DataHistory/Index.vue');
const TestItem = () => import('@/views/Test/TestItem/Index.vue');
const TestItemBatch = () => import('@/views/Test/TestItemBatch/Index.vue');
const TestTemplate = () => import('@/views/Test/TestTemplate/Index.vue');
const TestCase = () => import('@/views/Test/TestCase/Index.vue');
const UserList = () => import('@/views/UserManagement/UserList.vue');
const RolePermission = () => import('@/views/UserManagement/RolePermission.vue');

const routeScrollPositions = {};

function getWindowScrollPosition() {
  return {
    left: window.pageXOffset || document.documentElement.scrollLeft || document.body.scrollLeft || 0,
    top: window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0
  };
}

const routes = [
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
        path: '/UserList',
        name: 'UserList',
        component: UserList
      },
      {
        path: '/RolePermission',
        name: 'RolePermission',
        component: RolePermission
      },
      {
        path: '/404',
        component: () => import('@/views/errorPage/404'), 
        hidden: true
      },
      {
        path: '/401',
        component: () => import('@/views/errorPage/401'),
        hidden: true
      }
    ]
  },
  // 其他路由配置
];

const router = createRouter({
  history: createWebHashHistory(),
  routes,
  scrollBehavior(to, from, savedPosition) {
    if (savedPosition) {
      return savedPosition;
    }

    if (to.hash) {
      return {
        el: to.hash
      };
    }

    return routeScrollPositions[to.fullPath] || { left: 0, top: 0 };
  }
});

router.beforeEach((to, from, next) => {
  if (from.fullPath) {
    routeScrollPositions[from.fullPath] = getWindowScrollPosition();
  }

  next();
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
