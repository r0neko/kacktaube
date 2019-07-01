using System.Threading;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using osu.Framework.Platform;

namespace Pisstaube.CacheDb
{
    // Copy paste of https://github.com/ppy/osu/blob/master/osu.Game/Database/DatabaseContextFactory.cs
    public class PisstaubeCacheDbContextFactory
    {
        private readonly Storage storage;

        private const string database_name = @"cache";

        private ThreadLocal<PisstaubeCacheDbContext> threadContexts;

        private readonly object writeLock = new object();

        private bool currentWriteDidWrite;
        private bool currentWriteDidError;

        private int currentWriteUsages;

        private IDbContextTransaction currentWriteTransaction;

        public PisstaubeCacheDbContextFactory(Storage storage)
        {
            this.storage = storage;
            recycleThreadContexts();
        }
        
        public PisstaubeCacheDbContext Get() => threadContexts.Value;

        public DBWriteUsage GetForWrite(bool withTransaction = true)
        {
            Monitor.Enter(writeLock);
            PisstaubeCacheDbContext context;

            try
            {
                if (currentWriteTransaction == null && withTransaction)
                { 
                    if (threadContexts.IsValueCreated)
                        recycleThreadContexts();

                    context = threadContexts.Value;
                    currentWriteTransaction = context.Database.BeginTransaction();
                }
                else
                {
                    context = threadContexts.Value;
                }
            }
            catch
            {
                Monitor.Exit(writeLock);
                throw;
            }

            Interlocked.Increment(ref currentWriteUsages);

            return new DBWriteUsage(context, usageCompleted) { IsTransactionLeader = currentWriteTransaction != null && currentWriteUsages == 1 };
        }

        private void usageCompleted(DBWriteUsage usage)
        {
            var usages = Interlocked.Decrement(ref currentWriteUsages);

            try
            {
                currentWriteDidWrite |= usage.PerformedWrite;
                currentWriteDidError |= usage.Errors.Any();

                if (usages == 0)
                {
                    if (currentWriteDidError)
                        currentWriteTransaction?.Rollback();
                    else
                        currentWriteTransaction?.Commit();

                    if (currentWriteDidWrite || currentWriteDidError)
                    {
                        usage.Context.Dispose();
                        recycleThreadContexts();
                    }

                    currentWriteTransaction = null;
                    currentWriteDidWrite = false;
                    currentWriteDidError = false;
                }
            }
            finally
            {
                Monitor.Exit(writeLock);
            }
        }

        private void recycleThreadContexts()
        {
            threadContexts?.Value.Dispose();
            threadContexts = new ThreadLocal<PisstaubeCacheDbContext>(CreateContext, true);
        }

        protected virtual PisstaubeCacheDbContext CreateContext() => new PisstaubeCacheDbContext(storage.GetDatabaseConnectionString(database_name))
        {
            Database = { AutoTransactionsEnabled = false }
        };

        public void ResetDatabase()
        {
            lock (writeLock)
            {
                recycleThreadContexts();
                storage.DeleteDatabase(database_name);
            }
        }
    }
}