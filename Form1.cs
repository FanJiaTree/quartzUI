using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuartUI1._0.Business;
using Serilog;

namespace QuartUI1._0
{
    public partial class Form1 : Form
    {
        public ScheduleEntity Entity { get; set; }
        public static Form1 formtest = null;
        public Form1()
        {
            InitializeComponent();
            if (formtest == null)
            {
                formtest = this;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Entity = new ScheduleEntity {
                JobName = "任务名称",
                JobGroup = "任务分组",
                Description = "描述",
                RequestUrl = "请求url",
                RequestParameters = "请求参数",
                Headers = "",
                IntervalSecond = 5,
                BeginTime = DateTime.Now.AddSeconds(10),
                RunTimes = 2,
            };
           (new SchedulerCenter().ScheduleJob(Entity)).GetAwaiter().GetResult();
        }
    }
}
