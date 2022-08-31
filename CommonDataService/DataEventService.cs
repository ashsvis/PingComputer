using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.ServiceModel;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Linq;

namespace CommonDataService
{
    public class DataEventService : IDataEventService
    {
        private static readonly ConcurrentDictionary<string, DataItem> Cashprops = new ConcurrentDictionary<string, DataItem>();

        /// <summary>Рассылка всех значений из накопленного кэша сервера вновь подключившемуся клиенту</summary>
        /// <param name="clientId">ID клиента</param>
        public void SubscribeValues(Guid clientId)
        {
            ThreadPool.QueueUserWorkItem(param =>
            {
                foreach (var key in Cashprops.Keys)
                {
                    if (!Cashprops.TryGetValue(key, out DataItem item)) continue;
                    CustomUpdateProperty(clientId, item.SnapTime, item.Category, item.Name, item.Prop, item.Value, true, true);
                }
            });
        }

        /// <summary>Запрос клиентом файла на сервере</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="filename">полное имя файла на сервере</param>
        /// <param name="fileId">ID файла</param>
        /// <remarks>Сервер сжимает и отсылает найденный файл клиенту блоками по 16 кБайт</remarks>
        public void GetFile(Guid clientId, string filename, Guid fileId)
        {
            new Thread(() =>
            {
                var block = 0;
                if (!File.Exists(filename)) return;
                using (var inputStream = File.OpenRead(filename))
                {
                    var zipname = Guid.NewGuid().ToString();
                    try
                    {
                        using (var outFile = File.Create(zipname))
                        {
                            using (var compress = new GZipStream(outFile, CompressionMode.Compress))
                            {
                                inputStream.CopyTo(compress);
                            }
                        }
                        using (var zipStream = File.OpenRead(zipname))
                        {
                            var md5Hasher = MD5.Create();
                            var md5 = md5Hasher.ComputeHash(zipStream);
                            var lenght = zipStream.Length;
                            long totalBytes = 0;
                            var buffer = new byte[16 * 1024]; // не изменять!
                            zipStream.Position = 0;
                            do
                            {
                                var readBytes = zipStream.Read(buffer, 0, buffer.Length);
                                if (readBytes == 0) break;
                                //--------------------------------------------------
                                lock (Workers.SyncRoot)
                                {
                                    foreach (var callbacks in from DictionaryEntry worker in Workers
                                                              where ((Worker)worker.Value).Category.Equals("filetransfer")
                                                              select ((Worker)worker.Value).Callbacks)
                                    {
                                        lock (callbacks)
                                        {
                                            var removing = new List<CallbackInfo>();
                                            foreach (
                                                var callbackInfo in
                                                    callbacks.Where(callbackInfo => callbackInfo.ClientId.Equals(clientId)))
                                            {
                                                try
                                                {
                                                    callbackInfo.ClientCallback.
                                                                 FileBlockReceived(fileId, lenght, block, readBytes,
                                                                                   buffer, md5);
                                                    block++;
                                                }
                                                catch (CommunicationObjectAbortedException)
                                                {
                                                    removing.Add(callbackInfo);
                                                }
                                                catch (CommunicationException ex)
                                                {
                                                    //Data.SendToErrorsLog("Ошибка при отправке кешированного значения клиенту: " + ex.Message);
                                                }
                                                catch (Exception ex)
                                                {
                                                    //Data.SendToErrorsLog("Ошибка в GetFile() класса AShEventService: " + ex.FullMessage());
                                                }
                                            }
                                            foreach (var callbackInfo in removing)
                                                callbacks.Remove(callbackInfo);
                                        }
                                    }
                                }
                                //-----------------------------------------
                                totalBytes += readBytes;
                            } while (totalBytes < lenght);
                        }
                    }
                    finally
                    {
                        File.Delete(zipname);
                    }
                }
            }).Start();
        }

        public void SendCommand(Guid clientId, string address, int code, ushort[] hregs)
        {
            //Data.RemoteClientSendCommand(address, code, hregs);
        }

        /// <summary>Запись значения свойства в словарь</summary>
        /// <param name="category">имя категории</param>
        /// <param name="name">имя объекта</param>
        /// <param name="prop">имя свойства</param>
        /// <param name="value">значение</param>
        private static bool SetPropValue(string category, string name, string prop, string value)
        {
            var key = GetCashpropsKey(category, name, prop);
            // одинаковые значения игнорируем
            if (Cashprops.TryGetValue(key, out DataItem item) && item.Value.Equals(value)) return false;
            // создаем новый объект хранения
            item = new DataItem
            {
                SnapTime = DateTime.Now,
                Category = category,
                Name = name,
                Prop = prop,
                Value = value
            };
            // добавляем или обновляем значение свойства
            Cashprops.AddOrUpdate(key, item,
                                  (akey, existingVal) =>
                                  {
                                      existingVal.Value = value;
                                      existingVal.SnapTime = DateTime.Now;
                                      return existingVal;
                                  });
            return true;
        }

        private static string GetCashpropsKey(string category, string name, string prop)
        {
            return string.Concat(category.Replace('\t', '_'), "\t",
                                 name.Replace('\t', '_'), "\t",
                                 prop.Replace('\t', '_'));
        }

        private class DataItem
        {
            public string Category { get; set; }
            public string Name { get; set; }
            public string Prop { get; set; }
            public string Value { get; set; }
            public DateTime SnapTime { get; set; }

            public DataItem()
            {
                Category = string.Empty;
                Name = string.Empty;
                Prop = string.Empty;
                Value = string.Empty;
                SnapTime = DateTime.MinValue;
            }
        }

        private class CallbackInfo
        {
            public IClientCallback ClientCallback;
            public Guid ClientId;
        }

        private class Worker
        {
            public readonly List<CallbackInfo> Callbacks = new List<CallbackInfo>();
            public string Category { get; set; }
        }

        private static readonly Hashtable Workers = new Hashtable();

        /// <summary>Регистрирует клиента на подписку</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="categories">строковый массив категорий для подписки</param>
        public void RegisterForUpdates(Guid clientId, string[] categories)
        {
            new Thread(current =>
            {
                lock (Workers.SyncRoot)
                {
                    var list = new List<string>(categories) { "filetransfer" };
                    foreach (var category in list)
                    {
                        // при необходимости создаем новый рабочий объект, добавляем его
                        // добавляем его в хэш-таблицу и запускаем в отдельном потоке
                        Worker worker;
                        if (!Workers.ContainsKey(category))
                        {
                            worker = new Worker { Category = category };
                            Workers[category] = worker;
                        }
                        // Получить рабочий объект для данной category и добавить
                        // прокси клиента в список обратных вызовов
                        worker = (Worker)Workers[category];
                        var callback = ((OperationContext)current).GetCallbackChannel<IClientCallback>();
                        lock (worker.Callbacks)
                        {
                            worker.Callbacks.Add(new CallbackInfo
                            {
                                ClientCallback = callback,
                                ClientId = clientId
                            });
                        }
                    }
                }
            }).Start(OperationContext.Current);
        }

        /// <summary>Отключает клиента от подписки</summary>
        /// <param name="clientId">ID клиента</param>
        public void Disconnect(Guid clientId)
        {
            ThreadPool.QueueUserWorkItem(param =>
            {
                lock (Workers.SyncRoot)
                {
                    foreach (var callbacks in from DictionaryEntry worker in Workers
                                              select ((Worker)worker.Value).Callbacks)
                    {
                        lock (callbacks)
                        {
                            var removing =
                                callbacks.Where(cbinfo => cbinfo.ClientId.Equals(clientId)).ToList();
                            foreach (var callback in removing) callbacks.Remove(callback);
                        }
                    }
                }
            });
        }

        /// <summary>Изменение значения свойства клиентом</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="category">имя категории</param>
        /// <param name="pointname">имя объекта</param>
        /// <param name="propname">имя свойства</param>
        /// <param name="value">значение</param>
        /// <param name="nocash">не запоминать в кеш сервера</param>
        public void UpdateProperty(Guid clientId, string category, string pointname, string propname, string value,
                                   bool nocash)
        {
            CustomUpdateProperty(clientId, DateTime.Now, category, pointname, propname, value, nocash, false);
        }

        /// <summary>Изменение значения свойства внутри сервера</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="snaptime"></param>
        /// <param name="category">имя категории</param>
        /// <param name="pointname">имя объекта</param>
        /// <param name="propname">имя свойства</param>
        /// <param name="value">значение</param>
        /// <param name="nocash">не запоминать в кеш сервера</param>
        /// <param name="self">передача значений только для ID клиента или всем кроме</param>
        private static void CustomUpdateProperty(Guid clientId, DateTime snaptime, string category, string pointname,
                                                 string propname,
                                                 string value,
                                                 bool nocash, bool self)
        {
            if (nocash || SetPropValue(category, pointname, propname, value))
            {
                ThreadPool.QueueUserWorkItem(param =>
                {
                    lock (Workers.SyncRoot)
                    {
                        foreach (var callbacks in from DictionaryEntry worker in Workers
                                                  where ((Worker)worker.Value).Category.Equals(category)
                                                  select ((Worker)worker.Value).Callbacks)
                        {
                            lock (callbacks)
                            {
                                var removing = new List<CallbackInfo>();
                                foreach (var callbackInfo in
                                         callbacks.Where(
                                            callbackInfo => !self && !callbackInfo.ClientId.Equals(clientId) ||
                                                            self && callbackInfo.ClientId.Equals(clientId)))
                                {
                                    try
                                    {
                                        callbackInfo.ClientCallback.PropertyUpdated(snaptime, category,
                                                                                    pointname, propname,
                                                                                    value);
                                    }
                                    catch (CommunicationObjectAbortedException)
                                    {
                                        removing.Add(callbackInfo);
                                    }
                                    catch (CommunicationException ex)
                                    {
                                        //Data.SendToErrorsLog("Ошибка при отправке кешированного значения клиенту: " + ex.Message);
                                    }
                                    catch (Exception ex)
                                    {
                                        //Data.SendToErrorsLog("Ошибка в CustomUpdateProperty() класса AShEventService: " + ex.FullMessage());
                                    }
                                }
                                foreach (var callbackInfo in removing) callbacks.Remove(callbackInfo);
                            }
                        }
                    }
                });
            }
        }
    }
}
