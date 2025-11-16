namespace EAPManage.DataBase
{
    using SqlSugar;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    public class SqliteHelper<T> where T : class, new()
    {
        private static SqlSugarScope _db;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionString">数据库连接字符串</param>
        public SqliteHelper(SqlSugarScope db)
        {
            _db = db;
        }

        /// <summary>
        /// 获取所有实体
        /// </summary>
        public List<T> GetAll()
        {
            return _db.Queryable<T>().ToList();
        }

        /// <summary>
        /// 根据ID获取实体
        /// </summary>
        public T GetById(object id)
        {
            return _db.Queryable<T>().InSingle(id);
        }

        /// <summary>
        /// 插入实体
        /// </summary>
        public bool Insert(T entity)
        {
            return _db.Insertable(entity).ExecuteCommandIdentityIntoEntity();
        }

        /// <summary>
        /// 批量插入
        /// </summary>
        public int Insert(List<T> entities)
        {
            return _db.Insertable(entities).ExecuteCommand();
        }

        /// <summary>
        /// 更新实体
        /// </summary>
        public bool Update(T entity)
        {
            return _db.Updateable(entity).ExecuteCommandHasChange();
        }

        /// <summary>
        /// 根据条件更新
        /// </summary>
        public int Update(Expression<Func<T, bool>> where, Expression<Func<T, T>> update)
        {
            return _db.Updateable<T>().SetColumns(update).Where(where).ExecuteCommand();
        }

        ///// <summary>
        ///// 根据条件更新字段
        ///// </summary>
        ///// <typeparam name="T">实体类型</typeparam>
        ///// <param name="helper">工具类实例</param>
        ///// <param name="where">条件表达式</param>
        ///// <param name="update">更新表达式</param>
        ///// <returns>受影响的行数</returns>
        //public int UpdateWhere<T>(this SqliteHelper<T> helper,
        //    Expression<Func<T, bool>> where,
        //    Expression<Func<T, T>> update) where T : class, new()
        //{
        //    return helper.Update(where, update);
        //}

        /// <summary>
        /// 删除实体
        /// </summary>
        public bool Delete(T entity)
        {
            return _db.Deleteable(entity).ExecuteCommandHasChange();
        }

        /// <summary>
        /// 根据ID删除
        /// </summary>
        public bool DeleteById(object id)
        {
            return _db.Deleteable<T>(id).ExecuteCommandHasChange();
        }

        /// <summary>
        /// 根据条件删除
        /// </summary>
        public int Delete(Expression<Func<T, bool>> where)
        {
            return _db.Deleteable<T>().Where(where).ExecuteCommand();
        }

        /// <summary>
        /// 查询列表
        /// </summary>
        public List<T> GetList(Expression<Func<T, bool>> where)
        {
            return _db.Queryable<T>().Where(where).ToList();
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        public PageResult<T> GetPageList(Expression<Func<T, bool>> where, int pageIndex, int pageSize)
        {
            var total = 0;
            var list = _db.Queryable<T>()
                .Where(where)
                .ToPageList(pageIndex, pageSize, ref total);

            return new PageResult<T>
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = total,
                Data = list
            };
        }

        /// <summary>
        /// 执行事务
        /// </summary>
        public bool ExecuteTransaction(Action<SqlSugarScope> action)
        {
            try
            {
                _db.Ado.BeginTran();
                action(_db);
                _db.Ado.CommitTran();
                return true;
            }
            catch
            {
                _db.Ado.RollbackTran();
                throw;
            }
        }
    }

    /// <summary>
    /// 分页结果类
    /// </summary>
    public class PageResult<T>
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public List<T> Data { get; set; }
    }
}
