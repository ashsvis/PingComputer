using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading;
using CommonDataClient;

namespace ConsoleTestPing
{
    internal class Program
    {
        static readonly BackgroundWorker worker = new BackgroundWorker() { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
        static ClientConnectionStatus status = ClientConnectionStatus.Closed;
        static readonly EventClient client = new EventClient();

        static void Main(string[] args)
        {
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerAsync();
            Console.ReadKey();
            worker.CancelAsync();
        }

        private static void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //выводим адрес с которого пришел ответ в текстовое поле на форме и делаем перенос строки
            var state = $"{e.UserState}".Split('\t');
            Console.WriteLine(e.UserState);
            if (status == ClientConnectionStatus.Opened)
                client.UpdateProperty("Ping", "IpAddress", state[0], state[1]);
        }

        private static void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            client.Connect("10.9.3.22", 9931, new[] { "Ping" }, PropertyUpdate, ShowError, ClientFileReceived, UpdateLocalConnectionStatus);
            client.SubscribeValues();
            while (!worker.CancellationPending)
            {
                byte[] buffer = Encoding.ASCII.GetBytes("test ping");
                List<string> serversList = new List<string>();
                serversList.Add("10.9.3.6");
                serversList.Add("10.9.3.22");
                serversList.Add("10.9.3.55");
                foreach (string server in serversList)
                {
                    Ping ping = new Ping();
                    PingOptions options = new PingOptions(64, true);
                    var reply = ping.Send(IPAddress.Parse(server), 500, buffer, options);
                    worker.ReportProgress(0, $"{server}\t{reply.Status}");
                }
                Thread.Sleep(5000);
            }
        }

        private static void UpdateLocalConnectionStatus(Guid clientId, string ipaddr, ClientConnectionStatus stat)
        {
            status = stat;
            Console.WriteLine(status);
        }

        private static void ShowError(string errormessage)
        {
            //errormessage;
        }

        private static void PropertyUpdate(DateTime servertime, string category, string pointname, string propname, string value)
        {
            //string.Format("{0} {1} {2} {3}", category, pointname, propname, value);
        }

        private static void ClientFileReceived(string tarfilename, int percent, bool complete)
        {
            //
        }
    }
}
