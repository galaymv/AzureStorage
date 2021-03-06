﻿using System;
using System.Threading.Tasks;
using Common.Extensions;
using Common.Log;
using Lykke.Common.Log;
using Lykke.SettingsReader;

namespace AzureStorage.Tables.Templates
{

    public interface INoSqlTableForSetup
    {
        Task<T> GetValueAsync<T>(string field, T defaultValue);
        Task SetValueAsync<T>(string field, T value);

    }

    public abstract class NoSqlTableForSetupAbstract : INoSqlTableForSetup
    {
        private readonly NoSqlSetupByPartition _tableStorage;

        protected NoSqlTableForSetupAbstract(NoSqlSetupByPartition tableStorage)
        {
            _tableStorage = tableStorage;
            Partition = DefaultPartition;
        }

        public const string DefaultPartition = "Setup";
        public string Partition { get; set; }
        public string Field { get; set; }

        public string this[string field]
        {
            get
            {
                return _tableStorage.GetValueAsync(Partition, field).RunSync();

            }
            set
            {
                _tableStorage.SetValueAsync(Partition, field, value).RunSync();
            }
        }

        public Task<T> GetValueAsync<T>(string field, T defaultValue)
        {
            return _tableStorage.GetValueAsync(Partition, field, defaultValue);
        }

        public Task SetValueAsync<T>(string field, T value)
        {
            return _tableStorage.SetValueAsync(Partition, field, value);
        }
    }

    public class NoSqlTableForSetup : NoSqlTableForSetupAbstract
    {
        [Obsolete]
        public NoSqlTableForSetup(IReloadingManager<string> connStr, string tableName, ILog log) :
            base(new AzureSetupByPartition(connStr, tableName, log))
        {
        }

        public NoSqlTableForSetup(IReloadingManager<string> connStr, string tableName, ILogFactory logFactory) :
            base(new AzureSetupByPartition(connStr, tableName, logFactory))
        {
        }
    }

    public class NoSqlTableForSetupInMemory : NoSqlTableForSetupAbstract
    {
        public NoSqlTableForSetupInMemory() :
            base(new NoSqlSetupByPartitionInMemory())
        {
        }

    }

}
