namespace UniversalDreamcastPatcher.Core
{
    public sealed class IpBin
    {
        private string _Disc;
        public string Disc
        {
            get { return _Disc; }
            set
            {
                // Trim whitespace
                var trimmed = value?.Trim();

                // Validate format: integer/integer
                if (!string.IsNullOrEmpty(trimmed))
                {
                    var parts = trimmed.Split('/');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out _) &&
                        int.TryParse(parts[1], out _))
                    {
                        _Disc = trimmed;  // Valid format
                    }
                    else
                    {
                        _Disc = "1/1";  // Invalid, use default
                    }
                }
                else
                {
                    _Disc = "1/1";  // Empty, use default
                }
            }
        }

        public string Region { get; set; }
        public bool Vga { get; set; }
        public string Version { get; set; }
        public string ReleaseDate { get; set; }
        public string Name { get; set; }
        public string CRC { get; set; }
        public string ProductNumber { get; set; }
        public bool IsDefaultIpBin { get; set; }
    }
}