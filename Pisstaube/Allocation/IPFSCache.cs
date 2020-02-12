using System;
using System.Threading.Tasks;
using Ipfs.Http;
using osu.Framework.Platform; 
 
namespace Pisstaube.Allocation 
{ 
    public class IpfsCache 
    { 
        private readonly Storage storage; 
        private readonly IpfsClient ipfs = new IpfsClient(Environment.GetEnvironmentVariable("IPFS_HOST")); 
 
        public IpfsCache(Storage storage) 
        { 
            this.storage = storage; 
        } 
         
        public async Task<string> CacheFile(string path) 
        { 
            try 
            { 
                var fileInfo = await ipfs.FileSystem.AddFileAsync(storage.GetFullPath(path)); 
             
                return fileInfo?.Id.Hash.ToBase58(); 
            } 
            catch 
            { 
                return ""; 
            } 
        } 
    } 
}
