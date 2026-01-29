using SqlSugar;

namespace MCS.Silo.Database
{
    [SugarTable("OrleansQuery")]
    public class OrleansQuery
    {
        [SugarColumn(IsPrimaryKey = true, Length = 64)]
        public string QueryKey { get; set; }

        [SugarColumn(ColumnDataType = "text")]
        public string QueryText { get; set; }
    }

    [SugarTable("OrleansStorage")]
    public class OrleansStorage
    {
        [SugarColumn(IsPrimaryKey = false)]
        public int GrainIdHash { get; set; }

        [SugarColumn(IsPrimaryKey = false)]
        public long GrainIdN0 { get; set; }

        [SugarColumn(IsPrimaryKey = false)]
        public long GrainIdN1 { get; set; }

        [SugarColumn(IsPrimaryKey = false)]
        public int GrainTypeHash { get; set; }

        [SugarColumn(Length = 512)]
        public string GrainTypeString { get; set; }

        [SugarColumn(Length = 512, IsNullable = true)]
        public string GrainIdExtensionString { get; set; }

        [SugarColumn(Length = 150)]
        public string ServiceId { get; set; }

        [SugarColumn(ColumnDataType = "bytea", IsNullable = true)]
        public byte[] PayloadBinary { get; set; }

        public DateTime ModifiedOn { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? Version { get; set; }
    }

    [SugarTable("OrleansMembershipVersionTable")]
    public class OrleansMembershipVersionTable
    {
        [SugarColumn(IsPrimaryKey = true, Length = 150)]
        public string DeploymentId { get; set; }

        public DateTime Timestamp { get; set; }

        public int Version { get; set; }
    }

    [SugarTable("OrleansMembershipTable")]
    public class OrleansMembershipTable
    {
        [SugarColumn(IsPrimaryKey = true, Length = 150)]
        public string DeploymentId { get; set; }

        [SugarColumn(IsPrimaryKey = true, Length = 45)]
        public string Address { get; set; }

        [SugarColumn(IsPrimaryKey = true)]
        public int Port { get; set; }

        [SugarColumn(IsPrimaryKey = true)]
        public int Generation { get; set; }

        [SugarColumn(Length = 150)]
        public string SiloName { get; set; }

        [SugarColumn(Length = 150)]
        public string HostName { get; set; }

        public int Status { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? ProxyPort { get; set; }

        [SugarColumn(IsNullable = true, ColumnDataType = "text")]
        public string SuspectTimes { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime IAmAliveTime { get; set; }
    }

    [SugarTable("OrleansRemindersTable")]
    public class OrleansRemindersTable
    {
        [SugarColumn(IsPrimaryKey = true, Length = 150)]
        public string ServiceId { get; set; }

        [SugarColumn(IsPrimaryKey = true, Length = 150)]
        public string GrainId { get; set; }

        [SugarColumn(IsPrimaryKey = true, Length = 150)]
        public string ReminderName { get; set; }

        public DateTime StartTime { get; set; }

        public long Period { get; set; }

        public int GrainHash { get; set; }

        public int Version { get; set; }
    }
}