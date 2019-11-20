using System.Threading;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using osu.Framework.Platform;

namespace Pisstaube.Database
{
    // Copy paste of https://github.com/ppy/osu/blob/master/osu.Game/Database/DatabaseContextFactory.cs
    public class PisstaubeDbContextFactory
    {
        private ThreadLocal<PisstaubeDbContext> _threadContexts;

        private readonly object _writeLock = new object();

        private bool _currentWriteDidWrite;
        private bool _currentWriteDidError;

        private int _currentWriteUsages;

        private IDbContextTransaction _currentWriteTransaction;

        public PisstaubeDbContextFactory()
        {
            RecycleThreadContexts();
        }

        public PisstaubeDbContext Get() => _threadContexts.Value;

        public DbWriteUsage GetForWrite(bool withTransaction = true)
        {
            Monitor.Enter(_writeLock);
            PisstaubeDbContext context;

            try
            {
                if (_currentWriteTransaction == null && withTransaction)
                {
                    if (_threadContexts.IsValueCreated)
                        RecycleThreadContexts();

                    context = _threadContexts.Value;
                    _currentWriteTransaction = context.Database.BeginTransaction();
                }
                else
                {
                    context = _threadContexts.Value;
                }
            }
            catch
            {
                Monitor.Exit(_writeLock);
                throw;
            }

            Interlocked.Increment(ref _currentWriteUsages);

            return new DbWriteUsage(context, UsageCompleted)
                {IsTransactionLeader = _currentWriteTransaction != null && _currentWriteUsages == 1};
        }

        private void UsageCompleted(DbWriteUsage usage)
        {
            var usages = Interlocked.Decrement(ref _currentWriteUsages);

            try
            {
                _currentWriteDidWrite |= usage.PerformedWrite;
                _currentWriteDidError |= usage.Errors.Any();

                if (usages == 0)
                {
                    if (_currentWriteDidError)
                        _currentWriteTransaction?.Rollback();
                    else
                        _currentWriteTransaction?.Commit();

                    if (_currentWriteDidWrite || _currentWriteDidError)
                    {
                        usage.Context.Dispose();
                        RecycleThreadContexts();
                    }

                    _currentWriteTransaction = null;
                    _currentWriteDidWrite = false;
                    _currentWriteDidError = false;
                }
            }
            finally
            {
                Monitor.Exit(_writeLock);
            }
        }

        private void RecycleThreadContexts()
        {
            _threadContexts?.Value.Dispose();
            _threadContexts = new ThreadLocal<PisstaubeDbContext>(CreateContext, true);
        }

        protected virtual PisstaubeDbContext CreateContext() => new PisstaubeDbContext
        {
            Database = {AutoTransactionsEnabled = false}
        };
    }
}