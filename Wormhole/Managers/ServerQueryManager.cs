using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NLog;
using SteamQueryNet;
using Torch.API;
using Torch.Managers;

namespace Wormhole.Managers
{
    public class ServerQueryManager : Manager
    {
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        public ServerQueryManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        public async Task<bool> IsServerFull(string address)
        {
            try
            {
                using var query = new ServerQuery(address);
                var info = await query.GetServerInfoAsync();

                return info.Players == info.MaxPlayers;
            }
            catch (Exception e)
            {
                Log.Fatal(e);
                return false;
            }
        }
    }
}