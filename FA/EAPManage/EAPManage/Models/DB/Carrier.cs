using System.Collections.Generic;
using System.Reflection.Emit;
using EAPManage.Models.Enum;
using Microsoft.EntityFrameworkCore;
using SqlSugar;
namespace EAPManage.Models.DB
{
    [SugarTable("CARRIER")]
    public class Carrier
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "CARRIER_ID", ColumnDataType = "TEXT")]
        public string CarrierId { get; set; }

        [SugarColumn(ColumnName = "SIZE", ColumnDataType = "INTEGER")]
        public int Size { get; set; }

        [SugarColumn(ColumnName = "IN_USE", ColumnDataType = "INTEGER")]
        public CarrierUseState In_use { get; set; }
    }
}
