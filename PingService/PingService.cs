using CommonDataClient;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.IO;

namespace PingService
{
    public partial class PingService : ServiceBase
    {
        static readonly EventClient client = new EventClient();
        static readonly BackgroundWorker worker = new BackgroundWorker() { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
        static ClientConnectionStatus status = ClientConnectionStatus.Closed;

        public PingService()
        {
            InitializeComponent();
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
        }

        private static void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            switch (e.ProgressPercentage)
            {
                case 0:
                    //выводим адрес с которого пришел ответ
                    var state = $"{e.UserState}".Split('\t');
                    if (status == ClientConnectionStatus.Opened)
                        client.UpdateProperty("Ping", "IpAddress", state[0], state[1]);
                    break;
                case 1:
                    client.UpdateProperty("ImLive", "PingService", "LocalTime", DateTime.Now.ToString("O"));
                    break;
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var mif = new MemIniFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PingService.ini"));
            var commonDataServiceIp = mif.ReadString("CommonDataService", "IpAddress", "127.0.0.1");
            var commonDataServicePort = mif.ReadInteger("CommonDataService", "IpPort", 80);
            client.Connect(commonDataServiceIp, commonDataServicePort, new[] { "Ping", "ImLive" }, PropertyUpdate, ShowError, ClientFileReceived, UpdateLocalConnectionStatus);
            client.SubscribeValues();
            while (!worker.CancellationPending)
            {
                byte[] buffer = Encoding.ASCII.GetBytes("test ping");
                List<string> serversList = new List<string>();
                serversList.AddRange(mif.ReadSectionKeys("ComputersForPing"));
                //serversList.Add("10.9.3.6");
                //serversList.Add("10.9.3.22");
                //serversList.Add("10.9.3.55");
                foreach (string server in serversList)
                {
                    Ping ping = new Ping();
                    PingOptions options = new PingOptions(64, true);
                    var reply = ping.Send(IPAddress.Parse(server), 500, buffer, options);
                    worker.ReportProgress(0, $"{server}\t{reply.Status}");
                }
                worker.ReportProgress(1);
                Thread.Sleep(5000);
            }
            client.Disconnect();
        }

        //public static void Ping()
        //{
        //    byte[] buffer = Encoding.ASCII.GetBytes("test ping");
        //    List<string> serversList = new List<string>();
        //    serversList.Add("10.9.3.6");
        //    serversList.Add("10.9.3.55");
        //    foreach (string server in serversList)
        //    {
        //        Ping ping = new Ping();
        //        PingOptions options = new PingOptions(64, true);
        //        var reply = ping.Send(IPAddress.Parse(server), 500, buffer, options);
        //        //выводим адрес с которого пришел ответ в текстовое поле на форме и делаем перенос строки
        //        //Console.WriteLine(e.Reply.Address.ToString());
        //        if (status == ClientConnectionStatus.Opened)
        //            client.UpdateProperty("Ping", "IpAddress", reply.Address.ToString(), reply.Status.ToString());
        //    }
        //}

        protected override void OnStart(string[] args)
        {
            //EventLog.WriteEntry("OnStart", System.Diagnostics.EventLogEntryType.Information);
            worker.RunWorkerAsync();
        }

        protected override void OnStop()
        {
            //EventLog.WriteEntry("OnStop", System.Diagnostics.EventLogEntryType.Information);
            worker.CancelAsync();
        }

        private void UpdateLocalConnectionStatus(Guid clientId, string ipaddr, ClientConnectionStatus stat)
        {
            status = stat;
            //EventLog.WriteEntry($"{status}", System.Diagnostics.EventLogEntryType.Information);

        }

        private void ShowError(string errormessage)
        {
            //errormessage;
            //EventLog.WriteEntry($"{errormessage}", System.Diagnostics.EventLogEntryType.Information);
        }

        private void PropertyUpdate(DateTime servertime, string category, string pointname, string propname, string value)
        {
            //string.Format("{0} {1} {2} {3}", category, pointname, propname, value);
        }

        private void ClientFileReceived(string tarfilename, int percent, bool complete)
        {
            //
        }

        //private static void Ping(List<string> serversList)
        //{
        //    try
        //    {
        //        //создаем класс управления событиями в потоке
        //        AutoResetEvent waiter = new AutoResetEvent(false);
        //        //записываем в массив байт сконвертированную в байтовый массив строку для отправки в пинг запросе
        //        byte[] buffer = Encoding.ASCII.GetBytes("test ping");
        //        //получаем имя хоста текущей машины
        //        //string host = Dns.GetHostName();
        //        //создаем список для адресов удаленных машин типа string
        //        //List<string> serversList = new List<string>();
        //        //получаем ip машины по имени хоста
        //        //разделяем ip машины на 4 составных части
        //        //IPAddress ip = IPAddress.Parse("10.9.3.55");
        //        //string[] ipParts = ip.ToString().Split('.');
        //        ////создаем массив строк для записи туда всех 254 ip для их последующего пингования
        //        //string[] ips = new string[254];
        //        ////для каждого из последнего элемента начиная с 1 заканчивая 254
        //        //for (int i = 1; i < 255; i++)
        //        //{
        //        //    //записываем в массив строк нужный ip для сканирования, собирая его из элементов, последний заменяя на текущее значение переменной i
        //        //    ips[i - 1] = ipParts[0] + '.' + ipParts[1] + '.' + ipParts[2] + '.' + i.ToString();
        //        //}
        //        ////для каждого элемента из массива ip
        //        //foreach (string ipToScan in ips)
        //        //{
        //        //    //пишем его в список адресов
        //        //    serversList.Add(ipToScan);
        //        //}

        //        //для каждого элемента из списка адресов
        //        foreach (string server in serversList)
        //        {
        //            //создаем класс пинг
        //            Ping ping = new Ping();
        //            //указываем на метод, который будет выполняться в результате получения ответа на асинхронный запрос пинга
        //            ping.PingCompleted += Ping_PingCompleted;
        //            //выставляем опции для пинга (непринципиально)
        //            PingOptions options = new PingOptions(64, true);
        //            //посылаем асинхронный пинг на адрес с таймаутом 500мс, в нем передаем массив байт сконвертированных их строки в начале кода, 
        //            //используем при передаче указанные опции, после обращаемся к классу управления событиями
        //            ping.SendAsync(IPAddress.Parse(server), 500, buffer, options, waiter);
        //        }
        //        //очищаем элемент на форму куда будут выводиться все успешные ипы.
        //    }
        //    catch (Exception ex)
        //    {
        //        //Console.WriteLine(ex.Message);
        //    }
        //}

        //private static void Ping_PingCompleted(object sender, PingCompletedEventArgs e)
        //{
        //    if (e.Reply.Status == IPStatus.Success)
        //    {
        //        //выводим адрес с которого пришел ответ в текстовое поле на форме и делаем перенос строки
        //        //Console.WriteLine(e.Reply.Address.ToString());
        //        client.UpdateProperty("Ping", "IpAddress", e.Reply.Address.ToString(), "Connected");

        //    }
        //    // Let the main thread resume.
        //    ((AutoResetEvent)e.UserState).Set();
        //}
    }
}
