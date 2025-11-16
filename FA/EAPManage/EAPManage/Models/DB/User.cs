using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using SqlSugar;
namespace EAPManage.Models.DB
{
    [SugarTable("USER")]
    // Models/User.cs
    public class User
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "ID", ColumnDataType = "INTEGER")]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "NAME", ColumnDataType = "TEXT")]
        public string Name { get; set; }

        [SugarColumn(ColumnName = "PASSWORD", ColumnDataType = "TEXT")]
        public string Password { get; set; }

        [SugarColumn(ColumnName = "ROLE", ColumnDataType = "INTEGER")]
        public int Role { get; set; }
    }
}
