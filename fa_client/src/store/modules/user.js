import { login } from "@/api/user"

const user = {
  state: {
    token: "",
    userInfo: {},
    info: {}
  },

  mutations: {
    SET_TOKEN: (state, token) => {
      state.token = token
    },
    SET_INFO: (state, info) => {
      state.info = info
    },
    SET_USERINFO: (state, userInfo) => {
      state.userInfo = userInfo
    }
  },

  actions: {
    // Login({ commit }, userInfo) {
    //   return login(userInfo).then(res=>{
    //     const cicroUserInfo = {
    //       permissionList: [],
    //       roleList: [],
    //       token: "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiItMSIsImh0dHA6Ly9zY2hlbWFzLnhtbHNvYXAub3JnL3dzLzIwMDUvMDUvaWRlbnRpdHkvY2xhaW1zL25hbWUiOiLnrqHnkIblkZgiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJlYXBhZG1pbiIsImlhdCI6IjE2Nzg3NjM3MTgiLCJuYmYiOiIxNjc4NzYzNzE4IiwiZXhwIjoiMTY3ODg1MDExOCIsImlzcyI6IkdMIiwiYXVkIjoiR2xvcnlzQURNIn0.y2SeVbQFISPla9Rxjg7-Fhlhgzhe9hyK7kiDOOKh9BE",
    //       user: {
    //         creator: "admin",
    //         department: "14",
    //         gender: "男",
    //         id: -1,
    //         isActive: 1,
    //         mesNo: "eapadmin",
    //         name: "管理员",
    //         password: "",
    //         workNo: "eapadmin"
    //       }
    //     }
    //     localStorage.setItem("UserInfo", JSON.stringify(cicroUserInfo))
    //   })
    // },
    // 登出
    Logout ({ commit, state }) {
      return new Promise((resolve, reject) => {
        localStorage.removeItem("UserInfo")
        localStorage.clear()
        commit("SET_TOKEN", "")
        resolve()
      })
    },
    GetUserInfo ({ commit }) {
      return new Promise((resolve, reject) => {
        // try {
        //   const result = JSON.parse(localStorage.getItem("UserInfo")) || {}
        //   if (!["eapadmin", "superAdmin", "1"].includes(result.user.workNo) && (!Array.isArray(result.permissionList) || result.permissionList.length === 0)) {
        //     return notice.error("当前用户未分配权限，请联系系统管理员。")
        //   }
        //   commit("SET_USERINFO", result.user)
        //   commit("SET_TOKEN", result.token)
        //   resolve(result)
        // } catch (error) {
        //   reject(error)
        // }
      })
    }
  }
}

export default user
