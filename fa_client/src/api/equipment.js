import request from '@/utils/request'

export function GetList (paramsData) {
    return request({
        url: '/Equipment',
        method: 'get',
        params: paramsData
      })
  }

  export function GetEqpInfo(paramsData) {
    return request({
        url: '/Equipment/GetEqpInfo',
        method: 'get',
        params: paramsData
      })
  }

  export function GetDashboardSnapshot(paramsData) {
    return request({
        url: '/Equipment/GetDashboardSnapshot',
        method: 'get',
        params: paramsData
      })
  }

export function Add(data) {
  return request({
    url: '/Equipment',
    method: 'post',
    data: data
  })
}

export function Edit(data) {
  return request({
    url: '/Equipment',
    method: 'put',
    data: data
  })
}

export function Delete(data) {
  return request({
    url: '/Equipment',
    method: 'delete',
    data: data
  })
}

export function Deletes(data) {
  return request({
    url: '/Equipment/Deletes',
    method: 'delete',
    data: data
  })
}

/**
 * 获取所有设备的循环跑片控制状态
 */
export function GetLoopProcessState() {
  return request({
    url: '/Equipment/GetLoopProcessState',
    method: 'get'
  })
}

/**
 * 设置单个LP的循环跑片状态
 * @param {string} eqpId - 设备ID
 * @param {number} portId - LP的PortID
 * @param {number} state - 0=正常运行, 1=停止, 2=暂停
 */
export function SetLoopProcessState(eqpId, portId, state) {
  return request({
    url: '/Equipment/SetLoopProcessState',
    method: 'post',
    data: { eqpId, portId, state }
  })
}

/**
 * 批量设置所有设备的循环跑片状态
 * @param {number} state - 0=正常运行, 1=停止, 2=暂停
 */
export function SetAllLoopProcessState(state) {
  return request({
    url: '/Equipment/SetAllLoopProcessState',
    method: 'post',
    data: { state }
  })
}

export function GetTransferJobConfig (eqpId) {
  return request({
    url: '/Equipment/GetTransferJobConfig',
    method: 'get',
    params: eqpId ? { eqpId } : {}
  })
}

export function SetTransferJobConfig (eqpId, ports) {
  return request({
    url: '/Equipment/SetTransferJobConfig',
    method: 'post',
    data: { eqpId, ports }
  })
}

export function GetConnectionConfig(eqpId) {
  return request({
    url: '/Equipment/GetConnectionConfig',
    method: 'get',
    params: { eqpId }
  })
}

export function SetConnectionConfig(eqpId, config) {
  return request({
    url: '/Equipment/SetConnectionConfig',
    method: 'put',
    data: { eqpId, config }
  })
}
