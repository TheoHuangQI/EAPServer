import request from '@/utils/request'

export function GetList (paramsData) {
    return request({
        url: '/Exception',
        method: 'get',
        params: paramsData
      })
  }


export function Edit(data) {
  return request({
    url: '/Exception',
    method: 'put',
    data: data
  })
}

export function Delete(data) {
  return request({
    url: '/Exception',
    method: 'delete',
    data: data
  })
}

export function Deletes(data) {
  return request({
    url: '/Exception/Deletes',
    method: 'delete',
    data: data
  })
}
