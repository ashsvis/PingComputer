using System;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace CommonDataService
{
    /*
     * Класс реализации запуска WCF-сервиса. 
     * Реализован с использованием шаблона Singleton
     */
    public sealed class WcfEventService
    {
        private readonly TimeSpan timeout = new TimeSpan(0, 1, 30);
        private static WcfEventService wcfEventService;
        private readonly ServiceHost svcHost;

        public static WcfEventService EventService
        {
            get
            {
                wcfEventService = wcfEventService ?? new WcfEventService();
                return wcfEventService;
            }
        }

        // Конструктор по умолчанию определяется как private
        private WcfEventService()
        {
            // Регистрация сервиса и его метаданных
            svcHost = new ServiceHost(typeof(DataEventService),
                                       new[]
                                           {
                                               new Uri("net.pipe://localhost/CommonDataEventServer"),
                                               new Uri("net.tcp://localhost:9931/CommonDataEventServer")
                                           });
            svcHost.AddServiceEndpoint(typeof(IDataEventService),
                                        new NetNamedPipeBinding(), "");
            svcHost.AddServiceEndpoint(typeof(IDataEventService),
                                        new NetTcpBinding
                                        {
                                            OpenTimeout = timeout,
                                            SendTimeout = timeout,
                                            ReceiveTimeout = timeout,
                                            CloseTimeout = timeout,
                                            Security = new NetTcpSecurity { Mode = SecurityMode.None }
                                        }, "");
            var behavior = new ServiceMetadataBehavior();
            svcHost.Description.Behaviors.Add(behavior);
            svcHost.AddServiceEndpoint(typeof(IMetadataExchange),
                                        MetadataExchangeBindings.CreateMexNamedPipeBinding(), "mex");
            svcHost.AddServiceEndpoint(typeof(IMetadataExchange),
                                        MetadataExchangeBindings.CreateMexTcpBinding(), "mex");
        }

        public void Start()
        {
            svcHost.Open();
        }

        public void Stop()
        {
            svcHost.Close();
        }

    }
}
