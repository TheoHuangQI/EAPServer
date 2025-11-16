import request from '@/utils/request'

export function GetLotList (paramsData) {
    return request({
        url: '/LotInfo/' + paramsData,
        method: 'get'
      })
  }

export function GetLotListIdle () {
    return request({
        url: '/LotInfo/Idle',
        method: 'get'
      })
}

export function GetPPIDList (paramsData) {
  return request({
      url: '/LotInfo/GetPPIDList/'+paramsData,
      method: 'get'
    })
}


export function AddLot(data) {
  return request({
    url: '/LotInfo',
    method: 'post',
    data: data
  })
}

export function EditLot(data) {
  return request({
    url: '/LotInfo',
    method: 'put',
    data: data
  })
}

export function DeleteLot(data) {
  return request({
    url: '/LotInfo',
    method: 'delete',
    data: data
  })
}
