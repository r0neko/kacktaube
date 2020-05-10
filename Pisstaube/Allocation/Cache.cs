using System;
using Microsoft.Extensions.Caching.Memory;
using osu.Framework.Development;

namespace Pisstaube.Allocation
{
    public class Cache
    {
        private readonly IMemoryCache _memoryCache;

        public Cache(IMemoryCache memoryCache) => _memoryCache = memoryCache;
        
        public void Set<T>(object key, T value, TimeSpan duration)
        {
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