using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Torch.API;
using Torch.Managers;
using VRage.Security;
using Wormhole.ViewModels;

namespace Wormhole.Managers
{
    public class WormholeDiscoveryManager : Manager
    {
        [DllImport("msvcrt", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        public static extern int rename(
            [MarshalAs(UnmanagedType.LPStr)]
            string oldpath,
            [MarshalAs(UnmanagedType.LPStr)]
            string newpath);
        
        private static readonly XmlSerializer DiscoverySerializer = new (typeof(WormholeDiscovery));
        
        private readonly FileSystemWatcher _watcher = new ()
        {
            NotifyFilter = NotifyFilters.FileName
        };

        public readonly ConcurrentDictionary<string, WormholeDiscovery> Discoveries = new ();
        
        public WormholeDiscoveryManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        public bool IsLocalGate(string name)
        {
            return Discoveries.SingleOrDefault(b => b.Value.Gates.Exists(c => c.Name == name)).Key
                       ?.Equals(Plugin.Instance.Config.ThisIp, StringComparison.OrdinalIgnoreCase) ??
                   throw new InvalidOperationException($"Could not find gate with name {name}! Naming mistake?");
        }

        public GateViewModel GetGateByName(string name, out string ownerIp)
        {
            foreach (var (ip, discovery) in Discoveries)
            {
                var gate = discovery.Gates.SingleOrDefault(b =>
                    b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (gate is null) continue;
                
                ownerIp = ip;
                return gate;
            }

            throw new InvalidOperationException($"Could not find gate with name {name}! Naming mistake?");
        }

        public override void Attach()
        {
            base.Attach();
            var path = Path.Combine(Plugin.Instance.Config.Folder, "admingates_discovery");
            foreach (var file in Directory.CreateDirectory(path).GetFiles("*.xml"))
            {
                ProcessItem(file.Name, file.FullName);
            }

            _watcher.Path = path;
            _watcher.Renamed += WatcherOnRenamed;
            _watcher.EnableRaisingEvents = true;
            EnsureLatestDiscovery(path);
        }

        public override void Detach()
        {
            base.Detach();
            _watcher.EnableRaisingEvents = false;
        }

        private void EnsureLatestDiscovery(string directoryPath)
        {
            var thisIp = Plugin.Instance.Config.ThisIp.Replace(':', ';');
            var file = Directory.EnumerateFiles(directoryPath, $"{thisIp}_*.xml").FirstOrDefault();
            
            using var stream = new MemoryStream();
            DiscoverySerializer.Serialize(stream, new WormholeDiscovery
            {
                Gates = Plugin.Instance.Config.WormholeGates.ToList()
            });
            var buffer = stream.ToArray();
            
            var hash = Md5.ComputeHash(buffer).ToLowerString();
            
            if (file != null && Path.GetFileNameWithoutExtension(file).Split('_')[1].Equals(hash, StringComparison.OrdinalIgnoreCase))
                return;
            
            if (file != null)
                File.Delete(file);

            file = Path.Combine(directoryPath, $"{thisIp}_{hash}");
            
            using (var fileStream = File.Create(file))
                fileStream.Write(buffer, 0, buffer.Length);
            
            rename(file, Path.Combine(directoryPath, $"{thisIp}_{hash}.xml"));
        }
        
        private void ProcessItem(string fileName, string path)
        {
            using var reader = File.OpenText(path);
            Discoveries[fileName.Split('_')[0].Replace(';', ':')] = (WormholeDiscovery) DiscoverySerializer.Deserialize(reader);
        }

        private void WatcherOnRenamed(object sender, RenamedEventArgs e)
        {
            ProcessItem(e.Name, e.FullPath);
        }
    }

    public class WormholeDiscovery
    {
        [XmlArrayItem("Gate")]
        public List<GateViewModel> Gates { get; set; } = new ();
    }
}