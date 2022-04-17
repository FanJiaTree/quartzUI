using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Net;
using System.Threading.Tasks;
using QuartUI1._0.Common;
using Serilog;
using Quartz.Util;
using Quartz.Impl.AdoJobStore;
using Quartz.Impl.AdoJobStore.Common;
using Quartz.Impl.Matchers;
using Quartz.Impl.Triggers;
using Quartz.Simpl;
using QuartUI1._0.Repositories;

namespace QuartUI1._0.Business
{
    public class SchedulerCenter
    {
        /// <summary>
        /// 数据连接
        /// </summary>
        private IDbProvider dbProvider;

        /// <summary>
        /// ADO 数据类型
        /// </summary>
        private string driverDelegateType;
        /// <summary>
        /// 调度器
        /// </summary>
        private IScheduler scheduler;

        public SchedulerCenter()
        {
            InitDriverDelegateType();

            dbProvider = new DbProvider(AppConfig.DbProviderName, AppConfig.ConnectionString);
          
        }
        /// <summary>
        /// 初始化DriverDelegateType
        /// </summary>
        private void InitDriverDelegateType()
        {
            switch (AppConfig.DbProviderName)
            {
                case "SQLite-Microsoft":
                case "SQLite":
                    driverDelegateType = typeof(SQLiteDelegate).AssemblyQualifiedName;
                    break;
                case "MySql":
                    driverDelegateType = typeof(MySQLDelegate).AssemblyQualifiedName;
                    break;
                case "OracleODPManaged":
                    driverDelegateType = typeof(OracleDelegate).AssemblyQualifiedName;
                    break;
                case "SqlServer":
                case "SQLServerMOT":
                    driverDelegateType = typeof(SqlServerDelegate).AssemblyQualifiedName;
                    break;
                case "Npgsql":
                    driverDelegateType = typeof(PostgreSQLDelegate).AssemblyQualifiedName;
                    break;
                case "Firebird":
                    driverDelegateType = typeof(FirebirdDelegate).AssemblyQualifiedName;
                    break;
                default:
                    throw new Exception("dbProviderName unreasonable");
            }
        }

        public async Task ScheduleJob(ScheduleEntity entity)
        {
            //开启调度器
            await StartScheduleAsync();
            //await scheduler.Start();
            try
            {
                var Dir = new Dictionary<string, string>();
                Dir.Add(Constant.RUNNUMBER, "0");
                Dir.Add(Constant.JobTypeEnum, ((int)entity.JobType).ToString());

                IJobConfigurator jobConfigurator = null;
                if (entity.JobType == JobTypeEnum.Url)
                {
                    jobConfigurator = JobBuilder.Create<HttpJob>();
                    Dir.Add(Constant.REQUESTURL, entity.RequestUrl);
                    Dir.Add(Constant.HEADERS, entity.Headers);
                    Dir.Add(Constant.REQUESTPARAMETERS, entity.RequestParameters);
                    Dir.Add(Constant.REQUESTTYPE, ((int)entity.RequestType).ToString());
                }

                // 定义这个工作，并将其绑定到我们的IJob实现类                
                IJobDetail job = jobConfigurator
                    .SetJobData(new JobDataMap(Dir))
                    .WithDescription(entity.Description)
                    .WithIdentity(entity.JobName, entity.JobGroup)
                    .Build();
                //job.JobDataMap.Add("formtest", form);

                // 创建触发器
                ITrigger trigger;
                //校验是否正确的执行周期表达式
                if (entity.TriggerType == TriggerTypeEnum.Cron)//CronExpression.IsValidExpression(entity.Cron))
                {
                    trigger = CreateCronTrigger(entity);
                }
                else
                {
                    trigger = CreateSimpleTrigger(entity);
                }

                // 告诉Quartz使用我们的触发器来安排作业
                await scheduler.ScheduleJob(job, trigger);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }


        /// <summary>
        /// 初始化Scheduler
        /// </summary>
        private async Task InitSchedulerAsync()
        {
            if (scheduler == null)
            {
                DBConnectionManager.Instance.AddConnectionProvider("default", dbProvider);
                var serializer = new JsonObjectSerializer();
                serializer.Initialize();
                var jobStore = new JobStoreTX
                {
                    DataSource = "default",
                    TablePrefix = "QRTZ_",
                    InstanceId = "AUTO",
                    DriverDelegateType = driverDelegateType,
                    ObjectSerializer = serializer,
                };
                DirectSchedulerFactory.Instance.CreateScheduler("bennyScheduler", "AUTO", new DefaultThreadPool(), jobStore);
                scheduler = await SchedulerRepository.Instance.Lookup("bennyScheduler");
            }
        }
        public async Task<bool> StartScheduleAsync()
        {
            //初始化数据库表结构
            await InitDBTableAsync();
            //初始化Scheduler
            await InitSchedulerAsync();
            //开启调度器
            if (scheduler.InStandbyMode)
            {
                await scheduler.Start();
                Log.Information("任务调度启动！");
            }
            return scheduler.InStandbyMode;
        }
        /// <summary>
        /// 初始化数据库表
        /// </summary>
        /// <returns></returns>
        private async Task InitDBTableAsync()
        {
            //如果不存在sqlite数据库，则创建
            //TODO 其他数据源...
            if (driverDelegateType.Equals(typeof(SQLiteDelegate).AssemblyQualifiedName) ||
                driverDelegateType.Equals(typeof(MySQLDelegate).AssemblyQualifiedName) ||
                driverDelegateType.Equals(typeof(SqlServerDelegate).AssemblyQualifiedName) ||
                driverDelegateType.Equals(typeof(PostgreSQLDelegate).AssemblyQualifiedName) ||
                driverDelegateType.Equals(typeof(OracleDelegate).AssemblyQualifiedName))
            {
                IRepositorie repositorie = RepositorieFactory.CreateRepositorie(driverDelegateType, dbProvider);
                await repositorie?.InitTable();
            }
        }

        /// <summary>
        /// 创建类型Simple的触发器
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private ITrigger CreateSimpleTrigger(ScheduleEntity entity)
        {
            //作业触发器
            if (entity.RunTimes.HasValue && entity.RunTimes > 0)
            {
                return TriggerBuilder.Create()
               .WithIdentity(entity.JobName, entity.JobGroup)
               .StartAt(entity.BeginTime)//开始时间
                                         //.EndAt(entity.EndTime)//结束数据
               .WithSimpleSchedule(x =>
               {
                   x.WithIntervalInSeconds(entity.IntervalSecond.Value)//执行时间间隔，单位秒
                        .WithRepeatCount(entity.RunTimes.Value)//执行次数、默认从0开始
                        .WithMisfireHandlingInstructionFireNow();
               })
               .ForJob(entity.JobName, entity.JobGroup)//作业名称
               .Build();
            }
            else
            {
                return TriggerBuilder.Create()
               .WithIdentity(entity.JobName, entity.JobGroup)
               .StartAt(entity.BeginTime)//开始时间
                                         //.EndAt(entity.EndTime)//结束数据
               .WithSimpleSchedule(x =>
               {
                   x.WithIntervalInSeconds(entity.IntervalSecond.Value)//执行时间间隔，单位秒
                        .RepeatForever()//无限循环
                        .WithMisfireHandlingInstructionFireNow();
               })
               .ForJob(entity.JobName, entity.JobGroup)//作业名称
               .Build();
            }

        }

        /// <summary>
        /// 创建类型Cron的触发器
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private ITrigger CreateCronTrigger(ScheduleEntity entity)
        {
            // 作业触发器
            return TriggerBuilder.Create()

                   .WithIdentity(entity.JobName, entity.JobGroup)
                   .StartAt(entity.BeginTime)//开始时间
                                             //.EndAt(entity.EndTime)//结束时间
                   .WithCronSchedule(entity.Cron, cronScheduleBuilder => cronScheduleBuilder.WithMisfireHandlingInstructionFireAndProceed())//指定cron表达式
                   .ForJob(entity.JobName, entity.JobGroup)//作业名称
                   .Build();
        }

    }
}
