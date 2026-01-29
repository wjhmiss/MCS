using Microsoft.Extensions.Logging;
using SqlSugar;
using Npgsql;
using System.Text;

namespace MCS.Silo.Database
{
    public class OrleansDatabaseInitializer
    {
        private readonly ILogger<OrleansDatabaseInitializer> _logger;
        private readonly ISqlSugarClient _db;
        private readonly string _connectionString;

        public OrleansDatabaseInitializer(ILogger<OrleansDatabaseInitializer> logger, ISqlSugarClient db, string connectionString)
        {
            _logger = logger;
            _db = db;
            _connectionString = connectionString;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("开始初始化 Orleans 数据库表...");

                var existingTables = _db.DbMaintenance.GetTableInfoList();
                var existingTableNames = existingTables.Select(t => t.Name).ToList();

                var tableNames = new[]
                {
                    "OrleansQuery",
                    "OrleansStorage",
                    "OrleansMembershipVersionTable",
                    "OrleansMembershipTable",
                    "OrleansRemindersTable"
                };

                foreach (var tableName in tableNames)
                {
                    if (!existingTableNames.Contains(tableName))
                    {
                        _logger.LogInformation("创建表: {TableName}", tableName);
                    }
                }

                _db.CodeFirst.InitTables(
                    typeof(OrleansQuery),
                    typeof(OrleansStorage),
                    typeof(OrleansMembershipVersionTable),
                    typeof(OrleansMembershipTable),
                    typeof(OrleansRemindersTable)
                );

                _logger.LogInformation("Orleans 数据库表初始化完成");

                await CreateOrleansFunctionsAndProceduresAsync();
                await InsertOrleansQueriesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化 Orleans 数据库表时发生错误");
                throw;
            }
        }

        private async Task CreateOrleansFunctionsAndProceduresAsync()
        {
            try
            {
                _logger.LogInformation("开始创建 Orleans 存储过程和函数...");

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var sqlScript = GetOrleansFunctionsAndProceduresScript();
                using var command = new NpgsqlCommand(sqlScript, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Orleans 存储过程和函数创建完成");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "创建 Orleans 存储过程和函数时发生警告（可能已存在）");
            }
        }

        private async Task InsertOrleansQueriesAsync()
        {
            try
            {
                _logger.LogInformation("开始插入 Orleans 查询定义...");

                var existingQueries = _db.Queryable<OrleansQuery>().Select(x => x.QueryKey).ToList();

                var queries = GetOrleansQueries();

                foreach (var query in queries)
                {
                    if (!existingQueries.Contains(query.QueryKey))
                    {
                        _db.Insertable(query).ExecuteCommand();
                        _logger.LogInformation("插入查询: {QueryKey}", query.QueryKey);
                    }
                }

                _logger.LogInformation("Orleans 查询定义插入完成");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "插入 Orleans 查询定义时发生警告");
            }
        }

        private string GetOrleansFunctionsAndProceduresScript()
        {
            var sb = new StringBuilder();

            sb.AppendLine("CREATE OR REPLACE FUNCTION writetostorage(");
            sb.AppendLine("    _grainidhash integer,");
            sb.AppendLine("    _grainidn0 bigint,");
            sb.AppendLine("    _grainidn1 bigint,");
            sb.AppendLine("    _graintypehash integer,");
            sb.AppendLine("    _graintypestring character varying,");
            sb.AppendLine("    _grainidextensionstring character varying,");
            sb.AppendLine("    _serviceid character varying,");
            sb.AppendLine("    _grainstateversion integer,");
            sb.AppendLine("    _payloadbinary bytea)");
            sb.AppendLine("    RETURNS TABLE(newgrainstateversion integer)");
            sb.AppendLine("    LANGUAGE 'plpgsql'");
            sb.AppendLine("AS $function$");
            sb.AppendLine("    DECLARE");
            sb.AppendLine("     _newGrainStateVersion integer := _GrainStateVersion;");
            sb.AppendLine("     RowCountVar integer := 0;");
            sb.AppendLine("    BEGIN");
            sb.AppendLine("        IF _GrainStateVersion IS NOT NULL THEN");
            sb.AppendLine("            UPDATE OrleansStorage");
            sb.AppendLine("                SET");
            sb.AppendLine("                    PayloadBinary = _PayloadBinary,");
            sb.AppendLine("                    ModifiedOn = (now() at time zone 'utc'),");
            sb.AppendLine("                    Version = Version + 1");
            sb.AppendLine("                WHERE");
            sb.AppendLine("                    GrainIdHash = _GrainIdHash AND _GrainIdHash IS NOT NULL");
            sb.AppendLine("                    AND GrainTypeHash = _GrainTypeHash AND _GrainTypeHash IS NOT NULL");
            sb.AppendLine("                    AND GrainIdN0 = _GrainIdN0 AND _GrainIdN0 IS NOT NULL");
            sb.AppendLine("                    AND GrainIdN1 = _GrainIdN1 AND _GrainIdN1 IS NOT NULL");
            sb.AppendLine("                    AND GrainTypeString = _GrainTypeString AND _GrainTypeString IS NOT NULL");
            sb.AppendLine("                    AND ((_GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString = _GrainIdExtensionString) OR _GrainIdExtensionString IS NULL AND GrainIdExtensionString IS NULL)");
            sb.AppendLine("                    AND ServiceId = _ServiceId AND _ServiceId IS NOT NULL");
            sb.AppendLine("                    AND Version IS NOT NULL AND Version = _GrainStateVersion AND _GrainStateVersion IS NOT NULL;");
            sb.AppendLine("            GET DIAGNOSTICS RowCountVar = ROW_COUNT;");
            sb.AppendLine("            IF RowCountVar > 0 THEN");
            sb.AppendLine("                _newGrainStateVersion := _GrainStateVersion + 1;");
            sb.AppendLine("            END IF;");
            sb.AppendLine("        END IF;");
            sb.AppendLine("        IF _GrainStateVersion IS NULL THEN");
            sb.AppendLine("            INSERT INTO OrleansStorage");
            sb.AppendLine("                (");
            sb.AppendLine("                    GrainIdHash,");
            sb.AppendLine("                    GrainIdN0,");
            sb.AppendLine("                    GrainIdN1,");
            sb.AppendLine("                    GrainTypeHash,");
            sb.AppendLine("                    GrainTypeString,");
            sb.AppendLine("                    GrainIdExtensionString,");
            sb.AppendLine("                    ServiceId,");
            sb.AppendLine("                    PayloadBinary,");
            sb.AppendLine("                    ModifiedOn,");
            sb.AppendLine("                    Version");
            sb.AppendLine("                )");
            sb.AppendLine("                SELECT");
            sb.AppendLine("                    _GrainIdHash,");
            sb.AppendLine("                    _GrainIdN0,");
            sb.AppendLine("                    _GrainIdN1,");
            sb.AppendLine("                    _GrainTypeHash,");
            sb.AppendLine("                    _GrainTypeString,");
            sb.AppendLine("                    _GrainIdExtensionString,");
            sb.AppendLine("                    _ServiceId,");
            sb.AppendLine("                    _PayloadBinary,");
            sb.AppendLine("                   (now() at time zone 'utc'),");
            sb.AppendLine("                    1");
            sb.AppendLine("                WHERE NOT EXISTS");
            sb.AppendLine("                 (");
            sb.AppendLine("                    SELECT 1");
            sb.AppendLine("                    FROM OrleansStorage");
            sb.AppendLine("                    WHERE");
            sb.AppendLine("                        GrainIdHash = _GrainIdHash AND _GrainIdHash IS NOT NULL");
            sb.AppendLine("                        AND GrainTypeHash = _GrainTypeHash AND _GrainTypeHash IS NOT NULL");
            sb.AppendLine("                        AND GrainIdN0 = _GrainIdN0 AND _GrainIdN0 IS NOT NULL");
            sb.AppendLine("                        AND GrainIdN1 = _GrainIdN1 AND _GrainIdN1 IS NOT NULL");
            sb.AppendLine("                        AND GrainTypeString = _GrainTypeString AND GrainTypeString IS NOT NULL");
            sb.AppendLine("                        AND ((_GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString = _GrainIdExtensionString) OR _GrainIdExtensionString IS NULL AND GrainIdExtensionString IS NULL)");
            sb.AppendLine("                        AND ServiceId = _ServiceId AND _ServiceId IS NOT NULL");
            sb.AppendLine("                 );");
            sb.AppendLine("            GET DIAGNOSTICS RowCountVar = ROW_COUNT;");
            sb.AppendLine("            IF RowCountVar > 0 THEN");
            sb.AppendLine("                _newGrainStateVersion := 1;");
            sb.AppendLine("            END IF;");
            sb.AppendLine("        END IF;");
            sb.AppendLine("        RETURN QUERY SELECT _newGrainStateVersion AS NewGrainStateVersion;");
            sb.AppendLine("    END");
            sb.AppendLine("$function$;");

            sb.AppendLine("CREATE OR REPLACE FUNCTION upsert_reminder_row(");
            sb.AppendLine("    ServiceIdArg character varying,");
            sb.AppendLine("    GrainIdArg character varying,");
            sb.AppendLine("    ReminderNameArg character varying,");
            sb.AppendLine("    StartTimeArg timestamptz,");
            sb.AppendLine("    PeriodArg bigint,");
            sb.AppendLine("    GrainHashArg integer");
            sb.AppendLine("  )");
            sb.AppendLine("  RETURNS TABLE(version integer) AS");
            sb.AppendLine("$func$");
            sb.AppendLine("DECLARE");
            sb.AppendLine("    VersionVar int := 0;");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    INSERT INTO OrleansRemindersTable");
            sb.AppendLine("        (");
            sb.AppendLine("            ServiceId,");
            sb.AppendLine("            GrainId,");
            sb.AppendLine("            ReminderName,");
            sb.AppendLine("            StartTime,");
            sb.AppendLine("            Period,");
            sb.AppendLine("            GrainHash,");
            sb.AppendLine("            Version");
            sb.AppendLine("        )");
            sb.AppendLine("        SELECT");
            sb.AppendLine("            ServiceIdArg,");
            sb.AppendLine("            GrainIdArg,");
            sb.AppendLine("            ReminderNameArg,");
            sb.AppendLine("            StartTimeArg,");
            sb.AppendLine("            PeriodArg,");
            sb.AppendLine("            GrainHashArg,");
            sb.AppendLine("            0");
            sb.AppendLine("    ON CONFLICT (ServiceId, GrainId, ReminderName)");
            sb.AppendLine("        DO UPDATE SET");
            sb.AppendLine("            StartTime = excluded.StartTime,");
            sb.AppendLine("            Period = excluded.Period,");
            sb.AppendLine("            GrainHash = excluded.GrainHash,");
            sb.AppendLine("            Version = OrleansRemindersTable.Version + 1");
            sb.AppendLine("    RETURNING");
            sb.AppendLine("        OrleansRemindersTable.Version INTO STRICT VersionVar;");
            sb.AppendLine("    RETURN QUERY SELECT VersionVar AS version;");
            sb.AppendLine("END");
            sb.AppendLine("$func$ LANGUAGE plpgsql;");

            sb.AppendLine("CREATE OR REPLACE FUNCTION delete_reminder_row(");
            sb.AppendLine("    ServiceIdArg character varying,");
            sb.AppendLine("    GrainIdArg character varying,");
            sb.AppendLine("    ReminderNameArg character varying,");
            sb.AppendLine("    VersionArg integer");
            sb.AppendLine("  )");
            sb.AppendLine("  RETURNS TABLE(row_count integer) AS");
            sb.AppendLine("$func$");
            sb.AppendLine("DECLARE");
            sb.AppendLine("    RowCountVar int := 0;");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    DELETE FROM OrleansRemindersTable");
            sb.AppendLine("    WHERE");
            sb.AppendLine("        ServiceId = ServiceIdArg AND ServiceIdArg IS NOT NULL");
            sb.AppendLine("        AND GrainId = GrainIdArg AND GrainIdArg IS NOT NULL");
            sb.AppendLine("        AND ReminderName = ReminderNameArg AND ReminderNameArg IS NOT NULL");
            sb.AppendLine("        AND Version = VersionArg AND VersionArg IS NOT NULL;");
            sb.AppendLine("    GET DIAGNOSTICS RowCountVar = ROW_COUNT;");
            sb.AppendLine("    RETURN QUERY SELECT RowCountVar;");
            sb.AppendLine("END");
            sb.AppendLine("$func$ LANGUAGE plpgsql;");

            return sb.ToString();
        }

        private List<OrleansQuery> GetOrleansQueries()
        {
            return new List<OrleansQuery>
            {
                new OrleansQuery
                {
                    QueryKey = "WriteToStorageKey",
                    QueryText = "select * from WriteToStorage(@GrainIdHash, @GrainIdN0, @GrainIdN1, @GrainTypeHash, @GrainTypeString, @GrainIdExtensionString, @ServiceId, @GrainStateVersion, @PayloadBinary);"
                },
                new OrleansQuery
                {
                    QueryKey = "ReadFromStorageKey",
                    QueryText = "SELECT PayloadBinary, (now() at time zone 'utc'), Version FROM OrleansStorage WHERE GrainIdHash = @GrainIdHash AND GrainTypeHash = @GrainTypeHash AND @GrainTypeHash IS NOT NULL AND GrainIdN0 = @GrainIdN0 AND @GrainIdN0 IS NOT NULL AND GrainIdN1 = @GrainIdN1 AND @GrainIdN1 IS NOT NULL AND GrainTypeString = @GrainTypeString AND GrainTypeString IS NOT NULL AND ((@GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString = @GrainIdExtensionString) OR @GrainIdExtensionString IS NULL AND GrainIdExtensionString IS NULL) AND ServiceId = @ServiceId AND @ServiceId IS NOT NULL"
                },
                new OrleansQuery
                {
                    QueryKey = "ClearStorageKey",
                    QueryText = "UPDATE OrleansStorage SET PayloadBinary = NULL, Version = Version + 1 WHERE GrainIdHash = @GrainIdHash AND @GrainIdHash IS NOT NULL AND GrainTypeHash = @GrainTypeHash AND @GrainTypeHash IS NOT NULL AND GrainIdN0 = @GrainIdN0 AND @GrainIdN0 IS NOT NULL AND GrainIdN1 = @GrainIdN1 AND @GrainIdN1 IS NOT NULL AND GrainTypeString = @GrainTypeString AND GrainTypeString IS NOT NULL AND ((@GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString IS NOT NULL AND GrainIdExtensionString = @GrainIdExtensionString) OR @GrainIdExtensionString IS NULL AND GrainIdExtensionString IS NULL) AND ServiceId = @ServiceId AND @ServiceId IS NOT NULL AND Version IS NOT NULL AND Version = @GrainStateVersion AND @GrainStateVersion IS NOT NULL Returning Version as NewGrainStateVersion"
                },
                new OrleansQuery
                {
                    QueryKey = "UpsertReminderRowKey",
                    QueryText = "SELECT * FROM upsert_reminder_row(@ServiceId, @GrainId, @ReminderName, @StartTime, @Period, @GrainHash);"
                },
                new OrleansQuery
                {
                    QueryKey = "ReadReminderRowsKey",
                    QueryText = "SELECT GrainId, ReminderName, StartTime, Period, Version FROM OrleansRemindersTable WHERE ServiceId = @ServiceId AND @ServiceId IS NOT NULL AND GrainId = @GrainId AND @GrainId IS NOT NULL;"
                },
                new OrleansQuery
                {
                    QueryKey = "ReadReminderRowKey",
                    QueryText = "SELECT GrainId, ReminderName, StartTime, Period, Version FROM OrleansRemindersTable WHERE ServiceId = @ServiceId AND @ServiceId IS NOT NULL AND GrainId = @GrainId AND @GrainId IS NOT NULL AND ReminderName = @ReminderName AND @ReminderName IS NOT NULL;"
                },
                new OrleansQuery
                {
                    QueryKey = "ReadRangeRows1Key",
                    QueryText = "SELECT GrainId, ReminderName, StartTime, Period, Version FROM OrleansRemindersTable WHERE ServiceId = @ServiceId AND @ServiceId IS NOT NULL AND GrainHash > @BeginHash AND @BeginHash IS NOT NULL AND GrainHash <= @EndHash AND @EndHash IS NOT NULL;"
                },
                new OrleansQuery
                {
                    QueryKey = "ReadRangeRows2Key",
                    QueryText = "SELECT GrainId, ReminderName, StartTime, Period, Version FROM OrleansRemindersTable WHERE ServiceId = @ServiceId AND @ServiceId IS NOT NULL AND ((GrainHash > @BeginHash AND @BeginHash IS NOT NULL) OR (GrainHash <= @EndHash AND @EndHash IS NOT NULL));"
                },
                new OrleansQuery
                {
                    QueryKey = "DeleteReminderRowKey",
                    QueryText = "SELECT * FROM delete_reminder_row(@ServiceId, @GrainId, @ReminderName, @Version);"
                },
                new OrleansQuery
                {
                    QueryKey = "DeleteReminderRowsKey",
                    QueryText = "DELETE FROM OrleansRemindersTable WHERE ServiceId = @ServiceId AND @ServiceId IS NOT NULL;"
                }
            };
        }
    }
}