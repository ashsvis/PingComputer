using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Timers;
using Binding = System.ServiceModel.Channels.Binding;

namespace CommonDataClient
{
    public enum ClientConnectionStatus
    {
        Closed,
        Opening,
        Opened,
        Faulted
    }

    public class EventClient
    {
        private CallbackHandler callback;
        private readonly Guid clientId = Guid.NewGuid();
        private string host;
        private int port;
        private string[] categories;
        private PropertyUpdateWrapper propertyUpdate;
        private ClientErrorWrapper showError;
        private System.Timers.Timer faultTimer;
        private ClientFileReceivedWrapper fileReceived;
        private ConnectionStatusWrapper connectionStatus;

        public Guid ClientId { get { return clientId; } }

        public void Connect(string host, int port, string[] categories, PropertyUpdateWrapper propertyUpdate,
                            ClientErrorWrapper showError, ClientFileReceivedWrapper fileReceived,
                            ConnectionStatusWrapper connectionStatus)
        {
            this.host = host;
            this.port = port;
            this.categories = categories;
            this.propertyUpdate = propertyUpdate;
            this.showError = showError;
            this.fileReceived = fileReceived;
            this.connectionStatus = connectionStatus;
            ThreadPool.QueueUserWorkItem(param =>
            {
                callback = new CallbackHandler(clientId, host, port, ConnectionStatus);
                Thread.Sleep(100);
                callback.RegisterForUpdates(categories, propertyUpdate, showError, fileReceived);
            });
            faultTimer = new System.Timers.Timer(15 * 1000) { AutoReset = false };
            faultTimer.Elapsed += Reconnecting;
        }

        private void Reconnecting(object sender, ElapsedEventArgs e)
        {
            if (showError != null)
            {
                var mess = "Попытка подключиться к серверу событий [" + host + "] после сбоя связи.";
                showError(mess);
            }
            Reconnect();
        }

        private void ConnectionStatus(Guid clientId, string ipaddr, ClientConnectionStatus status)
        {
            if (status == ClientConnectionStatus.Faulted)
            {
                if (showError != null)
                {
                    var mess = "Канал связи [" + ipaddr + "] перешёл в состояние \"Ошибка\"";
                    showError(mess);
                }
                faultTimer.Enabled = true;
            }
            connectionStatus?.Invoke(clientId, ipaddr, status);
        }

        public void UpdateProperty(string category, string pointname, string propname, string value,
                                          bool nocash = false)
        {
            if (callback != null)
                ThreadPool.QueueUserWorkItem(param =>
                    callback.UpdateProperty(category, pointname, propname, value, nocash));
        }

        public void SubscribeValues()
        {
            if (callback != null) callback.SubscribeValues();
        }

        public void Disconnect()
        {
            if (callback != null) new Thread(() => callback.Disconnect()).Start();
        }

        public void Reconnect(string host = null, int port = 0)
        {
            if (host != null)
            {
                this.host = host;
                this.port = port;
            }
            ThreadPool.QueueUserWorkItem(param =>
            {
                Disconnect();
                Thread.Sleep(500);
                Connect(this.host, this.port, categories, propertyUpdate, showError,
                    fileReceived, connectionStatus);
            });
        }

        /// <summary>Запрос клиентом файла на сервере</summary>
        /// <param name="source">полное имя файла для чтения на сервере</param>
        /// <param name="target">полное имя файла для записи на клиенте</param>
        public void GetFile(string source, string target)
        {
            if (callback != null)
            {
                callback.GetFile(source, target);
            }
        }

        public void SendCommand(string address, int command, ushort[] hregs)
        {
            if (callback != null)
            {
                callback.SendCommand(address, command, hregs);
            }
        }
    }

    public delegate void PropertyUpdateWrapper(
        DateTime servertime, string category, string pointname, string propname, string value);

    public delegate void ClientErrorWrapper(string errormessage);

    public delegate void ConnectionStatusWrapper(Guid clientId, string ipaddr, ClientConnectionStatus status);

    public delegate void ClientFileReceivedWrapper(string tarfilename, int percent, bool complete);

    public class CallbackHandler : DataServiceRef.IDataEventServiceCallback, IDisposable
    {
        private readonly Guid clientId;
        private readonly string host;
        private readonly TimeSpan timeout = new TimeSpan(0, 1, 30);
        private readonly InstanceContext site;
        private readonly Binding binding;
        private readonly ConnectionStatusWrapper connectionStatus;
        private readonly DataServiceRef.DataEventServiceClient proxy;
        private readonly ConcurrentDictionary<Guid, FileItem> cashfiles =
            new ConcurrentDictionary<Guid, FileItem>();

        public CallbackHandler(Guid clientId, string host, int port, ConnectionStatusWrapper connectionStatus)
        {
            this.clientId = clientId;
            this.host = host;
            this.connectionStatus = connectionStatus;
            var uri = (host == null || host.Trim().Length == 0 || host.ToLower().Equals("localhost") ||
                       host.Equals("127.0.0.1"))
                          ? "net.pipe://localhost/CommonDataEventServer"
                          : string.Format("net.tcp://{0}:{1}/CommonDataEventServer", host.Trim(), port);
            site = new InstanceContext(this);
            if (uri.StartsWith("net.tcp://"))
            {
                binding = new NetTcpBinding
                {
                    OpenTimeout = timeout,
                    SendTimeout = timeout,
                    ReceiveTimeout = timeout,
                    CloseTimeout = timeout,
                    Security = new NetTcpSecurity { Mode = SecurityMode.None }
                };
            }
            else
                binding = new NetNamedPipeBinding
                {
                    OpenTimeout = timeout,
                    SendTimeout = timeout,
                    ReceiveTimeout = timeout,
                    CloseTimeout = timeout
                };
            proxy = new DataServiceRef.DataEventServiceClient(site, binding, new EndpointAddress(uri));

            proxy.InnerDuplexChannel.Opened += (sender, args) =>
            {
                this.connectionStatus?.Invoke(this.clientId, this.host, ClientConnectionStatus.Opened);
                ThreadPool.QueueUserWorkItem(arg =>
                {
                    Thread.Sleep(1000);
                    SubscribeValues();
                });
            };

            proxy.InnerDuplexChannel.Opening += (sender, args) =>
            {
                this.connectionStatus?.Invoke(this.clientId, this.host, ClientConnectionStatus.Opening);
            };

            proxy.InnerDuplexChannel.Closed += (sender, args) =>
            {
                this.connectionStatus?.Invoke(this.clientId, this.host, ClientConnectionStatus.Closed);
            };

            proxy.InnerDuplexChannel.Faulted += (sender, args) =>
            {
                this.connectionStatus?.Invoke(this.clientId, this.host, ClientConnectionStatus.Faulted);
            };
        }

        private PropertyUpdateWrapper propertyUpdate;
        private ClientErrorWrapper showError;
        private ClientFileReceivedWrapper fileReceived;

        /// <summary>Регистрирует клиента на подписку</summary>
        /// <param name="categories">строковый массив категорий для подписки</param>
        /// <param name="propertyUpdate">делегат события при изменении значения свойства</param>
        /// <param name="showError">делегат события при ошибке</param>
        /// <param name="fileReceived">делегат события при получении файла</param>
        public bool RegisterForUpdates(string[] categories, PropertyUpdateWrapper propertyUpdate,
                                       ClientErrorWrapper showError = null,
                                       ClientFileReceivedWrapper fileReceived = null)
        {
            this.propertyUpdate = propertyUpdate;
            this.showError = showError;
            this.fileReceived = fileReceived;
            try
            {
                proxy.RegisterForUpdates(clientId, categories);
                return true;
            }
            catch (EndpointNotFoundException ex)
            {
                SendMessage("Ошибка подключения к [" + host + "]: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в RegisterForUpdates() для [" + host + "]: ", ex.Message);
                SendMessage(message);
                return false;
            }
        }

        /// <summary>Рассылка всех значений из накопленного кэша сервера вновь подключившемуся клиенту</summary>
        public void SubscribeValues()
        {
            try
            {
                if (proxy.State.Equals(CommunicationState.Opened))
                    proxy.SubscribeValues(clientId);
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в SubscribeValues() для [" + host + "]: ", ex.Message);
                SendMessage(message);
            }
        }

        /// <summary>Изменение значения свойства клиентом</summary>
        /// <param name="category">имя категории</param>
        /// <param name="pointname">имя объекта</param>
        /// <param name="propname">имя свойства</param>
        /// <param name="value">значение</param>
        /// <param name="nocash">не запоминать в кеш сервера</param>
        public void UpdateProperty(string category, string pointname, string propname, string value, bool nocash)
        {
            try
            {
                if (proxy.State == CommunicationState.Opened)
                    proxy.UpdateProperty(clientId, category, pointname, propname, value, nocash);
            }
            catch (CommunicationObjectFaultedException ex)
            {
                SendMessage("Ошибка в UpdateProperty() для [" + host + "]: " + ex.Message);
            }
            catch (CommunicationException ex)
            {
                SendMessage("Ошибка в UpdateProperty() для [" + host + "]: " + ex.Message);
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в UpdateProperty() для [" + host + "]: ", ex.Message);
                SendMessage(message);
            }
        }

        private void SendMessage(string message)
        {
            if (showError == null) return;
            try
            {
                showError(message);
            }
            catch (Exception ex)
            {
                SendMessage("Ошибка при выводе сообщения: " + ex.Message);
            }
        }

        public void PropertyUpdated(DateTime servertime, string category, string pointname, string propname,
                                    string value)
        {
            if (propertyUpdate == null) return;
            try
            {
                propertyUpdate(servertime, category, pointname, propname, value);
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в PropertyUpdated() для [" + host + "]: ", ex.Message);
                SendMessage(message);
            }
        }

        public void SendCommand(string address, int command, ushort[] hregs)
        {
            try
            {
                if (proxy.State == CommunicationState.Opened)
                    proxy.SendCommand(clientId, address, command, hregs);
            }
            catch (CommunicationObjectFaultedException ex)
            {
                SendMessage("Ошибка в SendCommand() для [" + host + "]: " + ex.Message);
            }
            catch (CommunicationException ex)
            {
                SendMessage("Ошибка в SendCommand() для [" + host + "]: " + ex.Message);
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в SendCommand() для [" + host + "]: ", ex.Message);
                SendMessage(message);
            }

        }


        /// <summary>Запрос клиентом файла на сервере</summary>
        /// <param name="source">полное имя файла для чтения на сервере</param>
        /// <param name="target">полное имя файла для записи на клиенте</param>
        public void GetFile(string source, string target)
        {
            var fileId = Guid.NewGuid();
            try
            {
                if (!proxy.State.Equals(CommunicationState.Opened)) return;
                oldpercent = -1;
                var tmpname = Path.Combine(Path.GetDirectoryName(target) ?? "", fileId.ToString());
                var item = new FileItem
                {
                    Length = 0,
                    SourceFileName = source,
                    TargetFileName = target,
                    TempFileName = tmpname,
                    FileStream = File.Create(tmpname)
                };
                cashfiles.TryAdd(fileId, item);
                proxy.GetFile(clientId, source, fileId);
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в GetFile() для [" + host + "]: ", ex.Message);
                SendMessage(message);
            }
        }

        private int oldpercent;

        public void FileBlockReceived(Guid fileId, long length, int block, int size, byte[] buffer, byte[] md5)
        {
            FileItem item;
            if (!cashfiles.TryGetValue(fileId, out item)) return;
            var newitem = new FileItem
            {
                Length = item.Length += size,
                SourceFileName = item.SourceFileName,
                TargetFileName = item.TargetFileName,
                TempFileName = item.TempFileName,
                FileStream = item.FileStream,
            };
            cashfiles.TryUpdate(fileId, item, newitem);
            if (item.FileStream == null) return;
            if (!proxy.State.Equals(CommunicationState.Opened)) return;
            item.FileStream.Write(buffer, 0, size);
            var percent = Convert.ToInt32(100.0 * item.Length / length);
            if (fileReceived != null && percent != oldpercent)
            {
                try
                {
                    fileReceived(item.TargetFileName, percent, false);
                }
                catch (Exception ex)
                {
                    var message = string.Concat("Ошибка в FileBlockReceived() для [" + host + "]: ", ex.Message);
                    SendMessage(message);
                }
                oldpercent = percent;
            }
            if (!item.Length.Equals(length)) return;
            // Файл получен полностью
            item.FileStream.Position = 0;
            var md5Hasher = MD5.Create();
            var md5Data = md5Hasher.ComputeHash(item.FileStream);
            item.FileStream.Close();
            try
            {
                if (Md5ToString(md5) == Md5ToString(md5Data))
                {
                    var unzipname = Guid.NewGuid().ToString();
                    using (var inFile = File.OpenRead(item.TempFileName))
                    {
                        using (var outFile = File.Create(unzipname))
                        {
                            using (var decompress = new GZipStream(inFile, CompressionMode.Decompress))
                            {
                                decompress.CopyTo(outFile);
                            }
                        }
                    }
                    if (File.Exists(item.TargetFileName)) File.Delete(item.TargetFileName);
                    File.Move(unzipname, item.TargetFileName);
                    fileReceived?.Invoke(item.TargetFileName, 100, true);
                }
                else
                {
                    fileReceived?.Invoke(item.TargetFileName, 0, true);
                }
            }
            catch (Exception ex)
            {
                var message = String.Concat("Ошибка в FileBlockReceived() для [" + host + "]: ", ex.Message);
                SendMessage(message);
                if (fileReceived != null)
                    fileReceived(item.TargetFileName, 0, true);
            }
            finally
            {
                File.Delete(item.TempFileName);
            }
            cashfiles.TryRemove(fileId, out item);
        }

        private static string Md5ToString(byte[] data)
        {
            var sBuilder = new StringBuilder();
            for (var i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }

        public void Disconnect()
        {
            try
            {
                if (!proxy.State.Equals(CommunicationState.Opened)) return;
                DisposeTempFiles();
                proxy.Disconnect(clientId);
                proxy.InnerDuplexChannel.Close();
            }
            catch (Exception ex)
            {
                var message = string.Concat("Ошибка в Disconnect() для [" + host + "]: ", ex.Message);
                SendMessage(message);
            }
        }

        private void DisposeTempFiles()
        {
            foreach (var fileGuid in cashfiles.Keys)
            {
                FileItem item;
                if (!cashfiles.TryGetValue(fileGuid, out item)) continue;
                if (item.FileStream != null) item.FileStream.Close();
                if (File.Exists(item.TempFileName)) File.Delete(item.TempFileName);
            }
        }

        public void Dispose()
        {
            DisposeTempFiles();
        }
    }

    class FileItem
    {
        public long Length { get; set; }
        public string SourceFileName { get; set; }
        public string TargetFileName { get; set; }
        public string TempFileName { get; set; }
        public FileStream FileStream { get; set; }
    }
}
