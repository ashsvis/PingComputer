using CommonDataClient;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PingClient
{
    public partial class StatusForm : Form
    {
        ClientConnectionStatus status = ClientConnectionStatus.Closed;
        readonly EventClient client = new EventClient();
        DateTime pingservicetime = DateTime.Now;

        public StatusForm()
        {
            InitializeComponent();
        }

        private void StatusForm_Load(object sender, EventArgs e)
        {
            var mif = new MemIniFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PingClient.ini"));
            var commonDataServiceIp = mif.ReadString("CommonDataService", "IpAddress", "127.0.0.1");
            var commonDataServicePort = mif.ReadInteger("CommonDataService", "IpPort", 80);
            client.Connect(commonDataServiceIp, commonDataServicePort, new[] { "Ping", "ImLive" }, PropertyUpdate, ShowError, ClientFileReceived, UpdateLocalConnectionStatus);
            client.SubscribeValues();
        }

        private void UpdateLocalConnectionStatus(Guid clientId, string ipaddr, ClientConnectionStatus stat)
        {
            status = stat;
            var method = new MethodInvoker(() => { labStatus.Text = status.ToString(); });
            if (InvokeRequired)
                BeginInvoke(method);
            else
                method();
            if (status == ClientConnectionStatus.Opened)
            {
                method = new MethodInvoker(() => { lvNodes.Items.Clear(); });
                if (InvokeRequired)
                    BeginInvoke(method);
                else
                    method();
            }
            else
            {
                method = new MethodInvoker(() => { foreach (var lvi in lvNodes.Items.Cast<ListViewItem>()) lvi.SubItems[1].Text = "Unknown"; });
                if (InvokeRequired)
                    BeginInvoke(method);
                else
                    method();
            }
        }

        private void ShowError(string errormessage)
        {
            //Text = errormessage;
        }

        private void PropertyUpdate(DateTime servertime, string category, string pointname, string propname, string value)
        {
            var method = new MethodInvoker(() =>
            {
                switch (category)
                {
                    case "Ping":
                        var lvi = lvNodes.FindItemWithText(propname);
                        if (lvi == null)
                        {
                            lvi = new ListViewItem();
                            lvi.Text = propname;
                            lvi.SubItems.Add(value);
                            lvNodes.Items.Add(lvi);
                        }
                        else
                        {
                            lvi.SubItems[1].Text = value;
                        }
                        break;
                    case "ImLive":
                        if (DateTime.TryParse(value, out pingservicetime))
                        {
                            labPingServiceWorked.Text = "Success";
                        }
                        break;
                }
            });
            if (InvokeRequired)
                BeginInvoke(method);
            else
                method();
        }

        private void timerPingServiceLiveTimeout_Tick(object sender, EventArgs e)
        {
            labPingServiceWorked.Text = "Failure";
            foreach (var lvi in lvNodes.Items.Cast<ListViewItem>())
            {
                lvi.SubItems[1].Text = "Unknown";
            }
        }

        private void ClientFileReceived(string tarfilename, int percent, bool complete)
        {
            //
        }
    }
}
