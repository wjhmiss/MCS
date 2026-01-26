using MCS.Core.Entities;
using SqlSugar;

namespace MCS.Core.Data
{
    public class ApplicationDbContext
    {
        private readonly ISqlSugarClient _db;

        public ApplicationDbContext(ISqlSugarClient db)
        {
            _db = db;
        }

        public ISqlSugarClient Db => _db;

        public bool CreateDatabase()
        {
            return _db.DbMaintenance.CreateDatabase();
        }

        public void CreateTables()
        {
            _db.CodeFirst.InitTables(
                typeof(TaskDefinition),
                typeof(WorkflowDefinition),
                typeof(WorkflowNode),
                typeof(WorkflowConnection),
                typeof(TaskExecution),
                typeof(WorkflowExecution),
                typeof(Schedule),
                typeof(MQTTConfig),
                typeof(APIConfig),
                typeof(ExternalTrigger),
                typeof(Alert),
                typeof(SystemLog)
            );
        }
    }
}
