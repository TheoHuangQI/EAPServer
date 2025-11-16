import Vue from 'vue'
import Vuex from 'vuex'
import user from './modules/user'

Vue.use(Vuex)

const store =new Vuex.Store({
  getters: {
    token: state => state.user.token,
    userInfo: state => state.user.userInfo,
    userId: state => state.user.userInfo.workNo,
    routers: state => state.permission.routers,
  },
  modules: {
    user
  }
})
export default store