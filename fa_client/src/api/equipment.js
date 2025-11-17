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
