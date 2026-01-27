import request from '@/utils/request'

/**
 * 查询 SVID 历史趋势数据
 * @param {Object} paramsData
 * @param {string} paramsData.eqpId
 * @param {string} paramsData.startTime
 * @param {string} paramsData.endTime
 */
export function GetSvidTrend(paramsData) {
  return request({
    url: '/VIDValue',
    method: 'get',
    params: paramsData
  })
}

/**
 * 查询 异常SVID 历史趋势数据
 * @param {Object} paramsData
 * @param {string} paramsData.eqpId
 * @param {string} paramsData.startTime
 * @param {string} paramsData.endTime
 * @param {int} paramsData.abnormal
 */
export function GetSvidTrendwithAbnormal(paramsData) {
    return request({
      url: '/VIDValue/withAbnormal',
      method: 'get',
      params: paramsData
    })
  }