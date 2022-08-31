using System.ServiceProcess;

namespace CommonDataService
{
    public partial class DataWindowsService : ServiceBase
    {
        private readonly WcfEventService wcfEventService;

        public DataWindowsService()
        {
            InitializeComponent();
            wcfEventService = WcfEventService.EventService;
        }

        protected override void OnStart(string[] args)
        {
            wcfEventService.Start();
        }

        protected override void OnStop()
        {
            wcfEventService.Stop();
        }
    }
}
