using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using QuartUI1._0.Common;
using QuartUI1._0.IJobs.Model;
using QuartUI1._0.IJobs;
using QuartUI1._0;
using Talk.Extensions;
using QuartUI1._0.Model;

namespace QuartUI1._0
{
    public class HttpJob : JobBase<LogUrlModel>, IJob
    {
        public HttpJob() : base(new LogUrlModel())
        { }

        public override async Task NextExecute(IJobExecutionContext context)
        {
            //获取相关参数
            var requestUrl = context.JobDetail.JobDataMap.GetString(Constant.REQUESTURL)?.Trim();
            requestUrl = requestUrl?.IndexOf("http") == 0 ? requestUrl : "http://" + requestUrl;
            var requestParameters = context.JobDetail.JobDataMap.GetString(Constant.REQUESTPARAMETERS);
            var headersString = context.JobDetail.JobDataMap.GetString(Constant.HEADERS);
            var headers = headersString != null ? JsonConvert.DeserializeObject<Dictionary<string, string>>(headersString?.Trim()) : null;
            var requestType = (RequestTypeEnum)int.Parse(context.JobDetail.JobDataMap.GetString(Constant.REQUESTTYPE));


            LogInfo.Url = requestUrl;
            LogInfo.RequestType = requestType.ToString();
            LogInfo.Parameters = requestParameters;

            HttpResponseMessage response = new HttpResponseMessage();
            var http = HttpHelper.Instance;
            switch (requestType)
            {
                case RequestTypeEnum.Get:
                    response = await http.GetAsync(requestUrl, headers);
                    break;
                case RequestTypeEnum.Post:
                    response = await http.PostAsync(requestUrl, requestParameters, headers);
                    break;
                case RequestTypeEnum.Put:
                    response = await http.PutAsync(requestUrl, requestParameters, headers);
                    break;
                case RequestTypeEnum.Delete:
                    response = await http.DeleteAsync(requestUrl, headers);
                    break;
            }
            var result = HttpUtility.HtmlEncode(await response.Content.ReadAsStringAsync());
            LogInfo.Result = $"{result.MaxLeft(1000)}";
            if (!response.IsSuccessStatusCode)
            {
                LogInfo.ErrorMsg = $"{result.MaxLeft(3000)}";
                await ErrorAsync(LogInfo.JobName, new Exception(result.MaxLeft(3000)), JsonConvert.SerializeObject(LogInfo), MailLevel);
                context.JobDetail.JobDataMap[Constant.EXCEPTION] = $"{LogInfo.BeginTime}{JsonConvert.SerializeObject(LogInfo)}";
            }
            else
            {
                try
                {
                    //这里需要和请求方约定好返回结果约定为HttpResultModel模型
                    var httpResult = JsonConvert.DeserializeObject<HttpResultModel>(HttpUtility.HtmlDecode(result));
                    if (!httpResult.IsSuccess)
                    {
                        LogInfo.ErrorMsg = $"{httpResult.ErrorMsg}";
                        await ErrorAsync(LogInfo.JobName, new Exception(httpResult.ErrorMsg), JsonConvert.SerializeObject(LogInfo), MailLevel);
                        context.JobDetail.JobDataMap[Constant.EXCEPTION] = $"{LogInfo.BeginTime}{JsonConvert.SerializeObject(LogInfo)}";
                    }
                    else
                        await InformationAsync(LogInfo.JobName, JsonConvert.SerializeObject(LogInfo), MailLevel);
                }
                catch (Exception)
                {
                    await InformationAsync(LogInfo.JobName, JsonConvert.SerializeObject(LogInfo), MailLevel);
                }
            }
        }
    }
}
