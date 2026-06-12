import { createStore } from 'vuex'
import user from './modules/user'

const store = createStore({
  getters: {
    token: state => state.user.token,
    userInfo: state => state.user.userInfo,
    userId: state => state.user.userInfo.workNo,
    routers: state => state.permission?.routers || [],
  },
  modules: {
    user
  }
})
export default store