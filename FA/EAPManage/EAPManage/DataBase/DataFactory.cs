using EAPManage.Models.DB;
using SqlSugar;

namespace EAPManage.DataBase
{
    public class DataFactory
    {
        public static string SQLITE_FA_CONNSTR = $"{AppDomain.CurrentDomain.BaseDirectory}EAP.db"; //
        private static readonly Lazy<DataFactory> Lazy = new Lazy<DataFactory>(() => new DataFactory());
        public static DataFactory Current => Lazy.Value;
        public DataFactory()
        {
            string connectionString = $"DataSource={SQLITE_FA_CONNSTR}";
            if (_db == null)
            {
                _db = new SqlSugarScope(new ConnectionConfig()
                {
                    ConnectionString = connectionString,
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true
                });

                // 打印SQL语句（调试用）
                _db.Aop.OnLogExecuting = (sql, pars) =>
                {
                    Console.WriteLine(sql);
                };
                User = new SqliteHelper<User>(_db);
                Carrier = new SqliteHelper<Carrier>(_db);
                Lot = new SqliteHelper<LotInfo>(_db);
                Wafer = new SqliteHelper<Wafer>(_db);
                Batch = new SqliteHelper<Batch>(_db);
                Equipment = new SqliteHelper<Equipment>(_db);
                ExceptionRecord = new SqliteHelper<ExceptionRecord>(_db);
                PortInfo = new SqliteHelper<PortInfo>(_db);
                RunHistory = new SqliteHelper<RunHistory>(_db);
                DataRule = new SqliteHelper<DataRule>(_db);
                DataHistory = new SqliteHelper<DataHistory>(_db);
                TestItem = new SqliteHelper<TestItem>(_db);
                TestBatch = new SqliteHelper<TestBatch>(_db);
                TestBatchItem = new SqliteHelper<TestBatchItem>(_db);
                TestCase = new SqliteHelper<TestCase>(_db);
                TestTemplate = new SqliteHelper<TestTemplate>(_db);
                TestTemplateItem = new SqliteHelper<TestTemplateItem>(_db);
                TestHistory = new SqliteHelper<TestHistory>(_db);
            }
        }
        private SqlSugarScope _db;
        public  SqliteHelper<User> User;
        public  SqliteHelper<Carrier> Carrier;
        public  SqliteHelper<LotInfo> Lot;
        public  SqliteHelper<Wafer> Wafer;
        public  SqliteHelper<Batch> Batch;
        public  SqliteHelper<Equipment> Equipment;
        public SqliteHelper<ExceptionRecord> ExceptionRecord;
        public SqliteHelper<PortInfo> PortInfo;
        public SqliteHelper<RunHistory> RunHistory;
        public SqliteHelper<DataRule> DataRule;
        public SqliteHelper<DataHistory> DataHistory;
        public SqliteHelper<TestItem> TestItem;
        public SqliteHelper<TestBatch> TestBatch;
        public SqliteHelper<TestBatchItem> TestBatchItem;
        public SqliteHelper<TestCase> TestCase;
        public SqliteHelper<TestTemplate> TestTemplate;
        public SqliteHelper<TestTemplateItem> TestTemplateItem;
        public SqliteHelper<TestHistory> TestHistory;
        
    }
}
