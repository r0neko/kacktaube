using System.Threading.Tasks;
using Ipfs.Http;
using osu.Framework.Platform; 
 
namespace Pisstaube.Allocation 
{ 
    public class IPFSCache 
    { 
        private readonly Storage _storage; 
        private readonly IpfsClient ipfs = new IpfsClient(); 
 
        public IPFSCache(Storage storage) 
        { 
            _storage = storage; 
        } 
         
        public async Task<string> CacheFile(string path) 
        { 
            try 
            { 
                var fileInfo = await ipfs.FileSystem.AddFileAsync(_storage.GetFullPath(path)); 
             
                return fileInfo?.Id.Hash.ToBase58(); 
            } 
            catch 
            { 
                return ""; 
            } 
        } 
    } 
}
