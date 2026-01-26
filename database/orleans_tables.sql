CREATE TABLE IF NOT EXISTS "OrleansMembershipTable" (
    "DeploymentId" VARCHAR(150) NOT NULL,
    "Address" VARCHAR(45) NOT NULL,
    "Port" INT NOT NULL,
    "Generation" INT NOT NULL,
    "HostName" VARCHAR(150),
    "Status" INT NOT NULL,
    "ProxyPort" INT,
    "RoleName" VARCHAR(150),
    "SuspectTimes" VARCHAR(8000),
    "StartTime" TIMESTAMP,
    "IsActive" BOOLEAN NOT NULL,
    PRIMARY KEY ("DeploymentId", "Address", "Port", "Generation")
);

CREATE TABLE IF NOT EXISTS "OrleansMembershipTableVersion" (
    "DeploymentId" VARCHAR(150) NOT NULL,
    "Timestamp" TIMESTAMP NOT NULL,
    "MembershipVersion" BIGINT NOT NULL,
    PRIMARY KEY ("DeploymentId")
);

CREATE TABLE IF NOT EXISTS "OrleansReminderTable" (
    "ServiceId" VARCHAR(150) NOT NULL,
    "GrainId" VARCHAR(150) NOT NULL,
    "ReminderName" VARCHAR(150) NOT NULL,
    "StartTime" TIMESTAMP NOT NULL,
    "Period" BIGINT NOT NULL,
    "GrainHash" INT NOT NULL,
    "Version" BIGINT NOT NULL,
    PRIMARY KEY ("ServiceId", "GrainId", "ReminderName")
);

CREATE TABLE IF NOT EXISTS "OrleansQuery" (
    "GrainType" VARCHAR(512) NOT NULL,
    "GrainId" VARCHAR(512) NOT NULL,
    "GrainHash" INT NOT NULL,
    "ServiceId" VARCHAR(150) NOT NULL,
    PRIMARY KEY ("GrainType", "GrainId", "GrainHash")
);

CREATE TABLE IF NOT EXISTS "OrleansStorage" (
    "GrainType" VARCHAR(512) NOT NULL,
    "GrainId" VARCHAR(512) NOT NULL,
    "GrainHash" INT NOT NULL,
    "ServiceId" VARCHAR(150) NOT NULL,
    "Payload" BYTEA NOT NULL,
    "PayloadType" VARCHAR(512),
    "Version" BIGINT NOT NULL,
    "Timestamp" TIMESTAMP NOT NULL,
    PRIMARY KEY ("GrainType", "GrainId", "GrainHash")
);

CREATE INDEX IF NOT EXISTS "OrleansMembershipTable_DeploymentId_Index" ON "OrleansMembershipTable" ("DeploymentId");
CREATE INDEX IF NOT EXISTS "OrleansReminderTable_ServiceId_Index" ON "OrleansReminderTable" ("ServiceId");
CREATE INDEX IF NOT EXISTS "OrleansStorage_ServiceId_Index" ON "OrleansStorage" ("ServiceId");
