﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Lykke.AzureStorage;
using Lykke.AzureStorage.Tables.Paging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureStorage.Tables.Decorators
{
    /// <summary>
    /// Decorator, which adds retries functionality to atomic operations of <see cref="INoSQLTableStorage{T}"/> implementation
    /// </summary>
    /// <remarks>
    /// Methods without retries:
    /// - GetDataByChunksAsync
    /// - ScanDataAsync
    /// - FirstOrNullViaScanAsync
    /// - GetDataRowKeysOnlyAsync
    /// - ExecuteAsync
    /// - ExecuteQueryWithPaginationAsync
    /// </remarks>
    internal class RetryOnFailureAzureTableStorageDecorator<TEntity> : INoSQLTableStorage<TEntity> 
        where TEntity : ITableEntity, new()
    {
        public string Name => _impl.Name;

        private readonly INoSQLTableStorage<TEntity> _impl;
        private readonly int _onModificationsRetryCount;
        private readonly int _onGettingRetryCount;
        private readonly RetryService _retryService;

        /// <summary>
        /// Creates decorator, which adds retries functionality to atomic operations of <see cref="INoSQLTableStorage{T}"/> implementation
        /// </summary>
        /// <remarks>
        /// Methods without retries:
        /// - GetDataByChunksAsync
        /// - ScanDataAsync
        /// - FirstOrNullViaScanAsync
        /// - GetDataRowKeysOnlyAsync
        /// - ExecuteAsync
        /// </remarks>
        /// <param name="impl"><see cref="INoSQLTableStorage{T}"/> instance to which actual work will be delegated</param>
        /// <param name="onModificationsRetryCount">Retries count for write operations</param>
        /// <param name="onGettingRetryCount">Retries count for read operations</param>
        /// <param name="retryDelay">Delay before next retry. Default value is 200 milliseconds</param>
        public RetryOnFailureAzureTableStorageDecorator(
            INoSQLTableStorage<TEntity> impl, 
            int onModificationsRetryCount = 10, 
            int onGettingRetryCount = 10, 
            TimeSpan? retryDelay = null)
        {
            _impl = impl ?? throw new ArgumentNullException(nameof(impl));

            if (onModificationsRetryCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(onModificationsRetryCount), onModificationsRetryCount, "Value should be greater than 0");
            }

            if (onGettingRetryCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(onGettingRetryCount), onGettingRetryCount, "Value should be greater than 0");
            }

            _onModificationsRetryCount = onModificationsRetryCount;
            _onGettingRetryCount = onGettingRetryCount;
            _retryService = new RetryService(
                retryDelay: retryDelay ?? TimeSpan.FromMilliseconds(200),
                exceptionFilter: e =>
                {
                    switch (e)
                    {
                        case OptimisticConcurrencyException _:
                            return RetryService.ExceptionFilterResult.ThrowImmediately;

                        case StorageException storageException:
                        {
                            var noRetryStatusCodes = new[]
                            {
                                HttpStatusCode.Conflict,
                                HttpStatusCode.BadRequest,
                                HttpStatusCode.PreconditionFailed
                            };

                            return noRetryStatusCodes.Contains((HttpStatusCode) storageException.RequestInformation.HttpStatusCode)
                                ? RetryService.ExceptionFilterResult.ThrowImmediately
                                : RetryService.ExceptionFilterResult.ThrowAfterRetries;
                        }
                    }

                    return RetryService.ExceptionFilterResult.ThrowAfterRetries;
                });
        }

        public IEnumerator<TEntity> GetEnumerator() => _retryService.Retry(_impl.GetEnumerator, _onGettingRetryCount);

        IEnumerator IEnumerable.GetEnumerator() => _retryService.Retry(((IEnumerable)_impl).GetEnumerator, _onGettingRetryCount);

        TEntity INoSQLTableStorage<TEntity>.this[string partition, string row] => _retryService.Retry(() => _impl[partition, row], _onGettingRetryCount);

        IEnumerable<TEntity> INoSQLTableStorage<TEntity>.this[string partition] => _retryService.Retry(() => _impl[partition], _onGettingRetryCount);

        public async Task InsertAsync(TEntity item, params int[] notLogCodes)
        {
            await _retryService.RetryAsync(async () => await _impl.InsertAsync(item, notLogCodes), _onModificationsRetryCount);
        }

        public async Task InsertAsync(IEnumerable<TEntity> items)
        {
            await _retryService.RetryAsync(async () => await _impl.InsertAsync(items), _onModificationsRetryCount);
        }

        public async Task InsertOrMergeAsync(TEntity item)
        {
            await _retryService.RetryAsync(async () => await _impl.InsertOrMergeAsync(item), _onModificationsRetryCount);
        }

        public async Task InsertOrMergeBatchAsync(IEnumerable<TEntity> items)
        {
            await _retryService.RetryAsync(async () => await _impl.InsertOrMergeBatchAsync(items), _onModificationsRetryCount);
        }

        public async Task<TEntity> ReplaceAsync(string partitionKey, string rowKey, Func<TEntity, TEntity> replaceAction)
        {
            return await _retryService.RetryAsync(async () => await _impl.ReplaceAsync(partitionKey, rowKey, replaceAction), _onModificationsRetryCount);
        }

        public Task ReplaceAsync(TEntity entity)
        {
            return _retryService.RetryAsync(() => _impl.ReplaceAsync(entity), _onModificationsRetryCount);
        }

        public async Task<TEntity> MergeAsync(string partitionKey, string rowKey, Func<TEntity, TEntity> mergeAction)
        {
            return await _retryService.RetryAsync(async () => await _impl.MergeAsync(partitionKey, rowKey, mergeAction), _onModificationsRetryCount);
        }

        public async Task InsertOrReplaceBatchAsync(IEnumerable<TEntity> entities)
        {
            await _retryService.RetryAsync(async () => await _impl.InsertOrReplaceBatchAsync(entities), _onModificationsRetryCount);
        }

        public async Task InsertOrReplaceAsync(TEntity item)
        {
            await _retryService.RetryAsync(async () => await _impl.InsertOrReplaceAsync(item), _onModificationsRetryCount);
        }

        public async Task InsertOrReplaceAsync(IEnumerable<TEntity> items)
        {
            await _retryService.RetryAsync(async () => await _impl.InsertOrReplaceAsync(items), _onModificationsRetryCount);
        }

        public Task<bool> InsertOrReplaceAsync(TEntity entity, Func<TEntity, bool> replaceCondition)
        {
            return _retryService.RetryAsync(() => _impl.InsertOrReplaceAsync(entity, replaceCondition), _onModificationsRetryCount);
        }

        public Task<bool> InsertOrModifyAsync(string partitionKey, string rowKey, Func<TEntity> create, Func<TEntity, bool> modify)
        {
            return _retryService.RetryAsync(() => _impl.InsertOrModifyAsync(partitionKey, rowKey, create, modify), _onModificationsRetryCount);
        }

        public async Task DeleteAsync(TEntity item)
        {
            await _retryService.RetryAsync(async () => await _impl.DeleteAsync(item), _onModificationsRetryCount);
        }

        public async Task<TEntity> DeleteAsync(string partitionKey, string rowKey)
        {
            return await _retryService.RetryAsync(async () => await _impl.DeleteAsync(partitionKey, rowKey), _onModificationsRetryCount);
        }

        public async Task<bool> DeleteIfExistAsync(string partitionKey, string rowKey)
        {
            return await _retryService.RetryAsync(async () => await _impl.DeleteIfExistAsync(partitionKey, rowKey), _onModificationsRetryCount);
        }

        public Task<bool> DeleteIfExistAsync(string partitionKey, string rowKey, Func<TEntity, bool> deleteCondition)
        {
            return _retryService.RetryAsync(() => _impl.DeleteIfExistAsync(partitionKey, rowKey, deleteCondition), _onModificationsRetryCount);
        }

        public async Task<bool> DeleteAsync()
        {
            return await _retryService.RetryAsync(async () => await _impl.DeleteAsync(), _onModificationsRetryCount);
        }

        public async Task DeleteAsync(IEnumerable<TEntity> items)
        {
            await _retryService.RetryAsync(async () => await _impl.DeleteAsync(items), _onModificationsRetryCount);
        }

        public async Task<bool> CreateIfNotExistsAsync(TEntity item)
        {
            return await _retryService.RetryAsync(async () => await _impl.CreateIfNotExistsAsync(item), _onModificationsRetryCount);
        }

        public bool RecordExists(TEntity item)
        {
            return _retryService.Retry(() => _impl.RecordExists(item), _onGettingRetryCount);
        }

        public Task<bool> RecordExistsAsync(TEntity item)
        {
            return _retryService.RetryAsync(() => _impl.RecordExistsAsync(item), _onGettingRetryCount);
        }

        public async Task<TEntity> GetDataAsync(string partition, string row)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetDataAsync(partition, row), _onGettingRetryCount);
        }

        public async Task<IList<TEntity>> GetDataAsync(Func<TEntity, bool> filter = null)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetDataAsync(filter), _onGettingRetryCount);
        }

        public async Task<IEnumerable<TEntity>> GetDataAsync(string partitionKey, IEnumerable<string> rowKeys, int pieceSize = 100, Func<TEntity, bool> filter = null)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetDataAsync(partitionKey, rowKeys, pieceSize, filter), _onGettingRetryCount);
        }

        public async Task<IEnumerable<TEntity>> GetDataAsync(IEnumerable<string> partitionKeys, int pieceSize = 100, Func<TEntity, bool> filter = null)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetDataAsync(partitionKeys, pieceSize, filter), _onGettingRetryCount);
        }

        public async Task<IEnumerable<TEntity>> GetDataAsync(IEnumerable<Tuple<string, string>> keys, int pieceSize = 100, Func<TEntity, bool> filter = null)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetDataAsync(keys, pieceSize, filter), _onGettingRetryCount);
        }

        public Task GetDataByChunksAsync(Func<IEnumerable<TEntity>, Task> chunks)
        {
            return _impl.GetDataByChunksAsync(chunks);
        }

        public Task GetDataByChunksAsync(TableQuery<TEntity> rangeQuery, Func<IEnumerable<TEntity>, Task> chunks)
        {
            return _impl.GetDataByChunksAsync(rangeQuery, chunks);
        }

        public Task GetDataByChunksAsync(Action<IEnumerable<TEntity>> chunks)
        {
            return _impl.GetDataByChunksAsync(chunks);
        }

        public Task GetDataByChunksAsync(TableQuery<TEntity> rangeQuery, Action<IEnumerable<TEntity>> chunks)
        {
            return _impl.GetDataByChunksAsync(rangeQuery, chunks);
        }

        public Task GetDataByChunksAsync(string partitionKey, Action<IEnumerable<TEntity>> chunks)
        {
            return _impl.GetDataByChunksAsync(partitionKey, chunks);
        }

        public Task GetDataByChunksAsync(string partitionKey, Func<IEnumerable<TEntity>, bool> chunkHandler)
        {
            return _impl.GetDataByChunksAsync(partitionKey, chunkHandler);
        }

        public async Task<(IEnumerable<TEntity> Entities, string ContinuationToken)> GetDataWithContinuationTokenAsync(TableQuery<TEntity> rangeQuery, string continuationToken)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetDataWithContinuationTokenAsync(rangeQuery, continuationToken), _onGettingRetryCount);
        }

        public Task ScanDataAsync(string partitionKey, Func<IEnumerable<TEntity>, Task> chunk)
        {
            return _impl.ScanDataAsync(partitionKey, chunk);
        }

        public Task ScanDataAsync(TableQuery<TEntity> rangeQuery, Func<IEnumerable<TEntity>, Task> chunk)
        {
            return _impl.ScanDataAsync(rangeQuery, chunk);
        }

        public Task<TEntity> FirstOrNullViaScanAsync(string partitionKey, Func<IEnumerable<TEntity>, TEntity> dataToSearch)
        {
            return _impl.FirstOrNullViaScanAsync(partitionKey, dataToSearch);
        }

        public async Task<IEnumerable<TEntity>> GetDataAsync(string partition, Func<TEntity, bool> filter = null)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetDataAsync(partition, filter), _onGettingRetryCount);
        }

        public async Task<TEntity> GetTopRecordAsync(string partition)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetTopRecordAsync(partition), _onGettingRetryCount);
        }

        public async Task<IEnumerable<TEntity>> GetTopRecordsAsync(string partition, int n)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetTopRecordsAsync(partition, n), _onGettingRetryCount);
        }

        public async Task<TEntity> GetTopRecordAsync(TableQuery<TEntity> query)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetTopRecordAsync(query), _onGettingRetryCount);
        }

        public async Task<IEnumerable<TEntity>> GetTopRecordsAsync(TableQuery<TEntity> query, int n)
        {
            return await _retryService.RetryAsync(async () => await _impl.GetTopRecordsAsync(query, n), _onGettingRetryCount);
        }

        public Task<IEnumerable<TEntity>> GetDataRowKeysOnlyAsync(IEnumerable<string> rowKeys)
        {
            return _impl.GetDataRowKeysOnlyAsync(rowKeys);
        }

        public async Task<IEnumerable<TEntity>> WhereAsyncc(TableQuery<TEntity> rangeQuery, Func<TEntity, Task<bool>> filter = null)
        {
            return await _retryService.RetryAsync(async () => await _impl.WhereAsyncc(rangeQuery, filter), _onGettingRetryCount);
        }

        public async Task<IEnumerable<TEntity>> WhereAsync(TableQuery<TEntity> rangeQuery, Func<TEntity, bool> filter = null)
        {
            return await _retryService.RetryAsync(async () => await _impl.WhereAsync(rangeQuery, filter), _onGettingRetryCount);
        }

        public Task ExecuteAsync(TableQuery<TEntity> rangeQuery, Action<IEnumerable<TEntity>> yieldResult, Func<bool> stopCondition = null)
        {
            return _impl.ExecuteAsync(rangeQuery, yieldResult, stopCondition);
        }

        public async Task DoBatchAsync(TableBatchOperation batch)
        {
            await _retryService.RetryAsync(async () => await _impl.DoBatchAsync(batch), _onModificationsRetryCount);
        }

        public Task<IPagedResult<TEntity>> ExecuteQueryWithPaginationAsync(TableQuery<TEntity> query, PagingInfo pagingInfo)
        {
            return _impl.ExecuteQueryWithPaginationAsync(query, pagingInfo);
        }

        public Task CreateTableIfNotExistsAsync()
        {
            return _impl.CreateTableIfNotExistsAsync();
        }
    }   
}
