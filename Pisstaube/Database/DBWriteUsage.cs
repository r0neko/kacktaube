using System;
using System.Collections.Generic;

namespace Pisstaube.Database
{
    public class DbWriteUsage : IDisposable
    {
        public readonly PisstaubeDbContext Context;
        private readonly Action<DbWriteUsage> _usageCompleted;

        public DbWriteUsage(PisstaubeDbContext context, Action<DbWriteUsage> onCompleted)
        {
            Context = context;
            _usageCompleted = onCompleted;
        }

        public bool PerformedWrite { get; private set; }

        private bool _isDisposed;
        public List<Exception> Errors = new List<Exception>();

        public bool IsTransactionLeader = false;

        protected void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            _isDisposed = true;

            try
            {
                PerformedWrite |= Context.SaveChanges() > 0;
            }
            catch (Exception e)
            {
                Errors.Add(e);
                throw;
            }
            finally
            {
                _usageCompleted?.Invoke(this);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DbWriteUsage()
        {
            Dispose(false);
        }
    }
}