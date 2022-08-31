﻿using System;
using System.ServiceModel;

namespace CommonDataService
{
    [ServiceContract(CallbackContract = typeof(IClientCallback))]
    public interface IDataEventService
    {
        /// <summary>Регистрирует клиента на подписку</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="categories">строковый массив категорий для подписки</param>
        [OperationContract(IsOneWay = true)]
        void RegisterForUpdates(Guid clientId, string[] categories);

        /// <summary>Изменение значения свойства клиентом</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="category">имя категории</param>
        /// <param name="pointname">имя объекта</param>
        /// <param name="propname">имя свойства</param>
        /// <param name="value">значение</param>
        /// <param name="nocash">не запоминать в кеш сервера</param>
        [OperationContract(IsOneWay = true)]
        void UpdateProperty(Guid clientId, string category, string pointname, string propname, string value, bool nocash);

        /// <summary>Отключает клиента от подписки</summary>
        /// <param name="clientId">ID клиента</param>
        [OperationContract(IsOneWay = true)]
        void Disconnect(Guid clientId);

        /// <summary>Рассылка всех значений из накопленного кэша сервера вновь подключившемуся клиенту</summary>
        /// <param name="clientId">ID клиента</param>
        [OperationContract(IsOneWay = true)]
        void SubscribeValues(Guid clientId);

        /// <summary>Запрос клиентом файла на сервере</summary>
        /// <param name="clientId">ID клиента</param>
        /// <param name="filename">имя файла</param>
        /// <param name="fileId">ID файла</param>
        [OperationContract(IsOneWay = true)]
        void GetFile(Guid clientId, string filename, Guid fileId);

        [OperationContract(IsOneWay = true)]
        void SendCommand(Guid clientId, string address, int code, ushort[] hregs);

    }
}
