using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Written by Derek Pascarella (ateam)

namespace UniversalDreamcastPatcher.Core
{
    public enum IpBinMediaType
    {
        GdRom,
        CdRom,
    }

    [Flags]
    public enum IpBinPeripherals : uint
    {
        None = 0,
        WindowsCe = 0x000_0001,
        VgaBox = 0x000_0010,
        OtherExpansions = 0x000_0100,
        PuruPuru = 0x000_0200,
        Microphone = 0x000_0400,
        MemoryCard = 0x000_0800,
        StandardPad = 0x000_1000,
        CButton = 0x000_2000,
        DButton = 0x000_4000,
        XButton = 0x000_8000,
        YButton = 0x001_0000,
        ZButton = 0x002_0000,
        ExpandedDpad = 0x004_0000,
        AnalogTriggerR = 0x008_0000,
        AnalogTriggerL = 0x010_0000,
        AnalogStickHorizontal = 0x020_0000,
        AnalogStickVertical = 0x040_0000,
        ExpandedAnalogHorizontal = 0x080_0000,
        ExpandedAnalogVertical = 0x100_0000,
        LightGun = 0x200_0000,
        Keyboard = 0x400_0000,
        Mouse = 0x800_0000,
    }

    public enum IpBinField
    {
        MediaType,
        DiscNumber,
        DiscCount,
        Region,
        Peripherals,
        ProductNumber,
        Version,
        ReleaseDate,
        BootFilename,
        MakerName,
        SoftwareTitle,
    }

    public enum ValidationSeverity
    {
        Block,
        Warn,
    }

    public sealed class ValidationIssue
    {
        public IpBinField Field { get; init; }
        public ValidationSeverity Severity { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    // Strongly-typed view of an IP.BIN's meta header. Values are stored
    // exactly as given - malformed inputs load fine and the editor flags
    // them via Validate(), rather than blowing up on parse.
    public sealed class IpBinMetadata
    {
        public const string HardwareIdConst = "SEGA SEGAKATANA ";
        public const string MakerIdConst = "SEGA ENTERPRISES";

        public IpBinMediaType MediaType { get; set; } = IpBinMediaType.GdRom;
        public int DiscNumber { get; set; } = 1;
        public int DiscCount { get; set; } = 1;

        public bool RegionJapan { get; set; }
        public bool RegionUsa { get; set; }
        public bool RegionEurope { get; set; }

        public IpBinPeripherals Peripherals { get; set; } = IpBinPeripherals.StandardPad;

        public string ProductNumber { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string BootFilename { get; set; } = "1ST_READ.BIN";
        public string MakerName { get; set; } = string.Empty;
        public string SoftwareTitle { get; set; } = string.Empty;

        private static readonly Regex ProductNumberRule = new(@"^[A-Z0-9\- ]+$", RegexOptions.Compiled);
        private static readonly Regex VersionRule = new(@"^V\d\.\d{3}$", RegexOptions.Compiled);
        private static readonly Regex DateRule = new(@"^\d{8}$", RegexOptions.Compiled);

        public List<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();

            // Product number: required, 1-10 chars, A-Z 0-9 dash only.
            CheckProductNumber(issues);
            // Version: required, exactly V#.### (6 chars).
            CheckVersion(issues);
            // Release date: required, exactly 8 digits, valid YYYYMMDD.
            CheckReleaseDate(issues);
            // Boot filename: required, 1-16 printable ASCII.
            CheckText(issues, IpBinField.BootFilename, BootFilename, 16, requireNonEmpty: true);
            // Maker name: required, 1-16 printable ASCII.
            CheckText(issues, IpBinField.MakerName, MakerName, 16, requireNonEmpty: true);
            // Game title: required, 1-128 printable ASCII.
            CheckText(issues, IpBinField.SoftwareTitle, SoftwareTitle, 128, requireNonEmpty: true);

            // Disc number / count physically can't encode outside 1-9
            // (single ASCII digit). The view's NumericUpDown clamps user
            // input, so this only fires on a malformed source IP.BIN.
            if (DiscNumber < 1 || DiscNumber > 9)
                issues.Add(new ValidationIssue
                {
                    Field = IpBinField.DiscNumber,
                    Severity = ValidationSeverity.Block,
                    Reason = "must be 1-9"
                });
            if (DiscCount < 1 || DiscCount > 9)
                issues.Add(new ValidationIssue
                {
                    Field = IpBinField.DiscCount,
                    Severity = ValidationSeverity.Block,
                    Reason = "must be 1-9"
                });
            if (DiscNumber >= 1 && DiscCount >= 1 && DiscNumber > DiscCount)
                issues.Add(new ValidationIssue
                {
                    Field = IpBinField.DiscNumber,
                    Severity = ValidationSeverity.Warn,
                    Reason = "disc number is greater than the disc count"
                });

            if (!RegionJapan && !RegionUsa && !RegionEurope)
                issues.Add(new ValidationIssue
                {
                    Field = IpBinField.Region,
                    Severity = ValidationSeverity.Warn,
                    Reason = "no regions are checked, which could cause boot issues"
                });

            if ((Peripherals & IpBinPeripherals.StandardPad) == 0)
                issues.Add(new ValidationIssue
                {
                    Field = IpBinField.Peripherals,
                    Severity = ValidationSeverity.Warn,
                    Reason = "standard pad is not checked"
                });

            return issues;
        }

        private void CheckProductNumber(List<ValidationIssue> issues)
        {
            var s = ProductNumber ?? string.Empty;
            if (s.Length == 0)
                issues.Add(Block(IpBinField.ProductNumber, "required"));
            else if (s.Length > 10)
                issues.Add(Block(IpBinField.ProductNumber, "must be 10 characters or fewer"));
            else if (HasNonAscii(s))
                issues.Add(Block(IpBinField.ProductNumber, "contains non-ASCII characters"));
            else if (!ProductNumberRule.IsMatch(s))
                issues.Add(Block(IpBinField.ProductNumber, "allowed characters are A-Z, 0-9, dash, and space"));
        }

        private void CheckVersion(List<ValidationIssue> issues)
        {
            var s = Version ?? string.Empty;
            if (s.Length == 0)
                issues.Add(Block(IpBinField.Version, "required"));
            else if (s.Length > 6)
                issues.Add(Block(IpBinField.Version, "must be 6 characters or fewer"));
            else if (HasNonAscii(s))
                issues.Add(Block(IpBinField.Version, "contains non-ASCII characters"));
            else if (!VersionRule.IsMatch(s))
                issues.Add(Block(IpBinField.Version, "must match V#.### (e.g. V1.000)"));
        }

        private void CheckReleaseDate(List<ValidationIssue> issues)
        {
            var s = ReleaseDate ?? string.Empty;
            if (s.Length == 0)
                issues.Add(Block(IpBinField.ReleaseDate, "required"));
            else if (s.Length > 16)
                issues.Add(Block(IpBinField.ReleaseDate, "must be 16 characters or fewer"));
            else if (HasNonAscii(s))
                issues.Add(Block(IpBinField.ReleaseDate, "contains non-ASCII characters"));
            else if (!DateRule.IsMatch(s) || !IsValidYmd(s))
                issues.Add(Block(IpBinField.ReleaseDate, "must be a valid YYYYMMDD date"));
        }

        private static void CheckText(List<ValidationIssue> issues, IpBinField field, string value, int maxLen, bool requireNonEmpty)
        {
            var s = value ?? string.Empty;
            if (requireNonEmpty && s.Length == 0)
                issues.Add(Block(field, "required"));
            else if (s.Length > maxLen)
                issues.Add(Block(field, $"must be {maxLen} characters or fewer"));
            else if (HasNonAscii(s))
                issues.Add(Block(field, "contains non-ASCII characters"));
        }

        private static ValidationIssue Block(IpBinField field, string reason) =>
            new() { Field = field, Severity = ValidationSeverity.Block, Reason = reason };

        private static bool HasNonAscii(string s)
        {
            foreach (var c in s)
                if (c < 0x20 || c > 0x7E) return true;
            return false;
        }

        public IpBinMetadata Copy() => (IpBinMetadata)MemberwiseClone();

        private static bool IsValidYmd(string ymd)
        {
            if (ymd.Length != 8) return false;
            return DateTime.TryParseExact(ymd, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _);
        }

    }
}
