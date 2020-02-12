using System;
using Microsoft.Extensions.Caching.Memory;

namespace Pisstaube.Allocation
{
    public class Cache
    {
        private readonly IMemoryCache memoryCache;

        public Cache(IMemoryCache memoryCache) => this.memoryCache = memoryCache;

        public static Cache New() => new Cache(new MemoryCache(new MemoryCacheOptions()));

        public void Set<T>(object key, T value, TimeSpan duration)
        {
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(duration);

            memoryCache.Set(key, value, cacheEntryOptions);
        }

        public T Get<T>(object key) where T : class
        {
            if (!memoryCache.TryGetValue(key, out var obj))
                return null;
            return (T) obj;
        }

        public bool TryGet<T>(object key, out T t) where T : class
        {
            memoryCache.TryGetValue<T>(key, out var obj);
            t = obj;
            return t != null;
        }
    }
}