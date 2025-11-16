import request from '@/utils/request'

export function GetCarrierList (paramsData) {
    return request({
        url: '/Carrier/' + paramsData,
        method: 'get'
      })
  }

export function GetCarrierListIdle () {
    return request({
        url: '/Carrier/Idle',
        method: 'get'
      })
  }

export function AddCarrier(data) {
  return request({
    url: '/Carrier',
    method: 'post',
    data: data
  })
}

export function EditCarrier(data) {
  return request({
    url: '/Carrier',
    method: 'put',
    data: data
  })
}

export function DeleteCarrier(data) {
  return request({
    url: '/Carrier',
    method: 'delete',
    data: data
  })
}
