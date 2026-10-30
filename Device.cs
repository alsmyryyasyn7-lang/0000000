namespace NoNetApp
{
    public class Device
    {
        public string IpAddress { get; set; }
        public string MacAddress { get; set; }
        public string Name { get; set; }
        public bool IsBlocked { get; set; } = false;
    }
}