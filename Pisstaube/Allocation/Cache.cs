using System;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using osu.Framework.Development;

namespace Pisstaube.Allocation
{
    public class Cache
    {
        private IMemoryCache _memoryCache;

        public Cache() => CreateCache();


        private void CreateCache()
        {
            _memoryCache?.Dispose();

            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        
        public void Set<T>(object key, T value, TimeSpan duration)
        {
            using var proc = Process.GetCurrentProcess();
            if (proc.PrivateMemorySize64 / ((2 ^ 20) * 10024 / 100f) > 6144)
            {
                // straight up free cache
                CreateCache();
            }
            
            if (DebugUtils.IsDebugBuild)
                duration = TimeSpan.FromMilliseconds(1); // Disable cache for Debug
            
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(duration);

            _memoryCache.Set(key, value, cacheEntryOptions);
        }

        public T Get<T>(object key) where T : class
        {
            if (!_memoryCache.TryGetValue(key, out var obj))
                return null;
            
            return (T) obj;
        }

        public bool TryGet<T>(object key, out T t) where T : class
        {
            _memoryCache.TryGetValue<T>(key, out var obj);
            t = obj;
            return t != null;
        }
    }
}