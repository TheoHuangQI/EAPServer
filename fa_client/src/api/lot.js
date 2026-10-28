import request from '@/utils/request'

export function GetLotList (paramsData) {
    return request({
        url: '/LotInfo/' + paramsData,
        method: 'get'
      })
  }

export function GetLotListIdle (eqpId) {
    return request({
        url: '/LotInfo/Idle',
        method: 'get',
        params: eqpId ? { eqpId } : undefined
      })
}

export function GetPPIDList (paramsData) {
  const isObject = paramsData && typeof paramsData === 'object'
  const lotId = isObject ? paramsData.lotId : paramsData
  const eqpId = isObject ? paramsData.eqpId : ''
  const url = lotId ? `/LotInfo/GetPPIDList/${encodeURIComponent(lotId)}` : '/LotInfo/GetPPIDList'
  return request({
      url,
      method: 'get',
      params: eqpId ? { eqpId } : undefined
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
