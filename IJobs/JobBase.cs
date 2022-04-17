using QuartUI1._0.Common;
using QuartUI1._0.IJobs.Model;
using Newtonsoft.Json;
using Quartz;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Drawing;

namespace QuartUI1._0.IJobs
{
    [DisallowConcurrentExecution]
    [PersistJobDataAfterExecution]
    public abstract class JobBase<T> where T : LogModel, new()
    {
        protected readonly int maxLogCount = 20;//最多保存日志数量  
        protected readonly int warnTime = 20;//接口请求超过多少秒记录警告日志 
        protected Stopwatch stopwatch = new Stopwatch();
        protected T LogInfo { get; private set; }
        protected MailMessageEnum MailLevel = MailMessageEnum.None;

        public JobBase(T logInfo)
        {
            LogInfo = logInfo;
        }

        public async Task Execute(IJobExecutionContext context)
        {

            var form = context.JobDetail.JobDataMap.Get("formtest") as Form1;
            //记录执行次数
            var runNumber = context.JobDetail.JobDataMap.GetLong(Constant.RUNNUMBER);
            context.JobDetail.JobDataMap[Constant.RUNNUMBER] = ++runNumber;
            form.richTextBox1.Invoke(new dostr(dostring), new object[] { "记录执行次数："+ runNumber, Color.White, form });
            //var logs = context.JobDetail.JobDataMap[Constant.LOGLIST] as List<string> ?? new List<string>();
            //if (logs.Count >= maxLogCount)
            //    logs.RemoveRange(0, logs.Count - maxLogCount);

            stopwatch.Restart(); //  开始监视代码运行时间
            try
            {
                LogInfo.BeginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                LogInfo.JobName = $"{context.JobDetail.Key.Group}.{context.JobDetail.Key.Name}";
                form.richTextBox1.Invoke(new dostr(dostring), new object[] { "开始监视代码运行时间：" + LogInfo.BeginTime, Color.White, form });
                await NextExecute(context);
            }
            catch (Exception ex)
            {
                LogInfo.ErrorMsg = $"{ex.Message}";
                context.JobDetail.JobDataMap[Constant.EXCEPTION] = $"{LogInfo.BeginTime}{JsonConvert.SerializeObject(LogInfo)}";
                await ErrorAsync(LogInfo.JobName, ex, JsonConvert.SerializeObject(LogInfo), MailLevel);
            }
            finally
            {
                stopwatch.Stop(); //  停止监视            
                double seconds = stopwatch.Elapsed.TotalSeconds;  //总秒数             
                LogInfo.EndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                if (seconds >= 1)
                    LogInfo.ExecuteTime = seconds + "秒";
                else
                    LogInfo.ExecuteTime = stopwatch.Elapsed.TotalMilliseconds + "毫秒";

                var classErr = string.IsNullOrWhiteSpace(LogInfo.ErrorMsg) ? "" : "error";
                //logs.Add($"{classErr}{LogInfo.BeginTime} 至 {LogInfo.EndTime}  【耗时】{LogInfo.ExecuteTime}{JsonConvert.SerializeObject(LogInfo)}");
                //context.JobDetail.JobDataMap[Constant.LOGLIST] = logs;
                if (seconds >= warnTime)//如果请求超过20秒，记录警告日志    
                {
                    await WarningAsync(LogInfo.JobName, "耗时过长 - " + JsonConvert.SerializeObject(LogInfo), MailLevel);
                }
                Log.Information(JsonConvert.SerializeObject(LogInfo));
                form.richTextBox1.Invoke(new dostr(dostring), new object[] { JsonConvert.SerializeObject(LogInfo), Color.Red, form });
            }
        }

        public abstract Task NextExecute(IJobExecutionContext context);

        public async Task WarningAsync(string title, string msg, MailMessageEnum mailMessage)
        {
            Log.Logger.Warning(msg);
            await Task.Run(() => { });          
        }

        public async Task InformationAsync(string title, string msg, MailMessageEnum mailMessage)
        {
            Log.Logger.Information(msg);
            await Task.Run(() => { });
        }

        public async Task ErrorAsync(string title, Exception ex, string msg, MailMessageEnum mailMessage)
        {
            Log.Logger.Error(ex, msg);
            await Task.Run(() => { });
        }

        private delegate void dostr(string msg, Color color, Form1 form);

        private void dostring(string msg, Color color, Form1 form)
        {
            msg += Environment.NewLine;
            form.richTextBox1.SelectionStart = form.richTextBox1.TextLength;
            form.richTextBox1.SelectionLength = 0;
            form.richTextBox1.SelectionColor = color;
            form.richTextBox1.AppendText(msg);
            form.richTextBox1.SelectionColor = form.richTextBox1.ForeColor;
        }
    }
}
