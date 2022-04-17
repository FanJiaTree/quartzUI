using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuartUI1._0.Repositories
{
    public interface IRepositorie
    {
        /// <summary>
        /// 初始化表结构
        /// </summary>
        /// <returns></returns>
        Task<int> InitTable();
        Task<bool> RemoveErrLogAsync(string jobGroup, string jobName);
    }
}
