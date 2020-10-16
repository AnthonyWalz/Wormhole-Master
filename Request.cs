namespace Wormhole
{
    public class Request
    {
        public bool PluginRequest { get; set; }
        public string[] Destinations { get; set; }
        public string Destination { get; set; }
    }
}