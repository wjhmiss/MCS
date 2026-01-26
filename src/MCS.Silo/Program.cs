using Orleans;
using Orleans.Hosting;
using Orleans.Configuration;
using Microsoft.AspNetCore.SignalR;
using MCS.Grains.Interfaces;
using MCS.Hubs;
using MCS.Core.Data;
using MCS.Core.Repositories;
using SqlSugar;
using Orleans.Storage;

var builder = WebApplication.CreateBuilder(args);

var connectionString = "Data Source=:memory:";

builder.Services.AddScoped<ISqlSugarClient>(s =>
{
    var db = new SqlSugarClient(new ConnectionConfig()
    {
        ConnectionString = connectionString,
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });

    db.Aop.OnLogExecuting = (sql, pars) =>
    {
        Console.WriteLine(sql + "\r\n" + db.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value)));
    };

    return db;
});

builder.Services.AddScoped<ApplicationDbContext>();
builder.Services.AddScoped<ITaskDefinitionRepository, TaskDefinitionRepository>();
builder.Services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
builder.Services.AddScoped<IWorkflowNodeRepository, WorkflowNodeRepository>();
builder.Services.AddScoped<IWorkflowConnectionRepository, WorkflowConnectionRepository>();
builder.Services.AddScoped<ITaskExecutionRepository, TaskExecutionRepository>();
builder.Services.AddScoped<IWorkflowExecutionRepository, WorkflowExecutionRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IAlertRepository, AlertRepository>();

builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering()
        .Configure<ClusterOptions>(options =>
        {
            options.ClusterId = "MCS-Cluster";
            options.ServiceId = "MCS-Service";
        })
        .AddMemoryGrainStorage("Default")
        .UseInMemoryReminderService()
        .Configure<GrainCollectionOptions>(options =>
        {
            options.CollectionAge = TimeSpan.FromMinutes(30);
        });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MCS Task Management API",
        Version = "v1",
        Description = "任务管理系统 API - 基于 Orleans 的分布式任务调度和执行系统"
    });
    
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.CreateDatabase();
    dbContext.CreateTables();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "MCS API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TaskHub>("/hubs/tasks");

await app.RunAsync();
