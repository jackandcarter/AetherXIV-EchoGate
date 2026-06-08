namespace EchoGate.Core;

public enum PatchRepository
{
    Boot,
    Game
}

public enum PatchLibraryInspectionMode
{
    PresenceOnly,
    Size,
    Checksum
}

public sealed record PatchEntry(
    PatchRepository Repository,
    string FromVersion,
    string ToVersion,
    long ExpectedSizeBytes,
    uint ExpectedCrc32)
{
    public string RepositoryId => Repository == PatchRepository.Boot ? "2d2a390f" : "48eca647";

    public string PatchFileName => $"D{ToVersion}.patch";

    public string TorrentFileName => $"D{ToVersion}.torrent";

    public string RelativePatchPath => Path.Combine("ffxiv", RepositoryId, "patch", PatchFileName);

    public string RelativeMetainfoPath => Path.Combine("ffxiv", RepositoryId, "metainfo", TorrentFileName);

    public string ExpectedCrc32Text => ExpectedCrc32.ToString("X8");
}

public sealed record PatchFileReport(
    PatchEntry Entry,
    string PatchPath,
    string MetainfoPath,
    bool PatchFileExists,
    bool MetainfoFileExists,
    long? ActualSizeBytes,
    uint? ActualCrc32)
{
    public bool SizeMatches => ActualSizeBytes == Entry.ExpectedSizeBytes;

    public bool Crc32Matches => ActualCrc32 == Entry.ExpectedCrc32;

    public bool IsPatchValid(PatchLibraryInspectionMode mode)
    {
        if (!PatchFileExists)
            return false;

        if (mode >= PatchLibraryInspectionMode.Size && !SizeMatches)
            return false;

        if (mode >= PatchLibraryInspectionMode.Checksum && !Crc32Matches)
            return false;

        return true;
    }
}

public sealed record PatchLibraryReport(
    string RootPath,
    IReadOnlyList<PatchEntry> ExpectedEntries,
    IReadOnlyList<PatchFileReport> FileReports,
    IReadOnlyList<PatchEntry> MissingPatchFiles,
    IReadOnlyList<PatchEntry> MissingMetainfoFiles,
    IReadOnlyList<PatchFileReport> InvalidPatchFiles,
    PatchLibraryInspectionMode InspectionMode)
{
    public int ExpectedPatchCount => ExpectedEntries.Count;

    public int PresentPatchCount => FileReports.Count(report => report.PatchFileExists);

    public int PresentMetainfoCount => FileReports.Count(report => report.MetainfoFileExists);

    public bool HasAllPatchFiles => MissingPatchFiles.Count == 0;

    public bool HasAllMetainfoFiles => MissingMetainfoFiles.Count == 0;

    public bool HasValidPatchFiles => InvalidPatchFiles.Count == 0;

    public bool IsComplete => HasAllPatchFiles && HasAllMetainfoFiles && HasValidPatchFiles;

    public string Summary => IsComplete
        ? InspectionMode switch
        {
            PatchLibraryInspectionMode.Checksum => $"complete: {ExpectedPatchCount} patches verified by CRC32 and metainfo files",
            PatchLibraryInspectionMode.Size => $"complete: {ExpectedPatchCount} patches with expected sizes and metainfo files",
            _ => $"complete: {ExpectedPatchCount} patches and metainfo files"
        }
        : $"patches {PresentPatchCount}/{ExpectedPatchCount}, metainfo {PresentMetainfoCount}/{ExpectedPatchCount}, invalid {InvalidPatchFiles.Count}";
}

public static class LegacyPatchManifest
{
    public static IReadOnlyList<PatchEntry> Entries { get; } = CreateEntries();

    public static PatchLibraryReport InspectLibrary(
        string rootPath,
        PatchLibraryInspectionMode inspectionMode = PatchLibraryInspectionMode.Size)
    {
        string normalizedRoot = string.IsNullOrWhiteSpace(rootPath)
            ? ""
            : Path.GetFullPath(rootPath);

        List<PatchFileReport> fileReports = new();
        List<PatchEntry> missingPatches = new();
        List<PatchEntry> missingMetainfo = new();
        List<PatchFileReport> invalidPatches = new();

        foreach (PatchEntry entry in Entries)
        {
            string patchPath = string.IsNullOrWhiteSpace(normalizedRoot)
                ? ""
                : Path.Combine(normalizedRoot, entry.RelativePatchPath);
            string metainfoPath = string.IsNullOrWhiteSpace(normalizedRoot)
                ? ""
                : Path.Combine(normalizedRoot, entry.RelativeMetainfoPath);
            bool patchExists = !string.IsNullOrWhiteSpace(patchPath) && File.Exists(patchPath);
            bool metainfoExists = !string.IsNullOrWhiteSpace(metainfoPath) && File.Exists(metainfoPath);
            long? actualSize = patchExists ? new FileInfo(patchPath).Length : null;
            uint? actualCrc32 = patchExists && inspectionMode >= PatchLibraryInspectionMode.Checksum
                ? Crc32.ComputeFile(patchPath)
                : null;

            PatchFileReport fileReport = new(
                entry,
                patchPath,
                metainfoPath,
                patchExists,
                metainfoExists,
                actualSize,
                actualCrc32);

            fileReports.Add(fileReport);

            if (!patchExists)
            {
                missingPatches.Add(entry);
            }

            if (!metainfoExists)
            {
                missingMetainfo.Add(entry);
            }

            if (patchExists && !fileReport.IsPatchValid(inspectionMode))
            {
                invalidPatches.Add(fileReport);
            }
        }

        return new PatchLibraryReport(
            normalizedRoot,
            Entries,
            fileReports,
            missingPatches,
            missingMetainfo,
            invalidPatches,
            inspectionMode);
    }

    private static IReadOnlyList<PatchEntry> CreateEntries()
    {
        List<PatchEntry> entries =
        [
            new(
                PatchRepository.Boot,
                ClientVersionInfo.BaseVersion,
                ClientVersionInfo.TargetBootVersion,
                5571687,
                0x47DDE5ED)
        ];

        (string Version, long SizeBytes, uint Crc32)[] gamePatches =
        [
            ("2010.09.19.0000", 444398866, 0xD55C7ACD),
            ("2010.09.23.0000", 6907277, 0xCA135D55),
            ("2010.09.28.0000", 18803280, 0xB19B32FE),
            ("2010.10.07.0001", 19226330, 0xD6118CEE),
            ("2010.10.14.0000", 19464329, 0x34BF6A99),
            ("2010.10.22.0000", 19778252, 0x2543DB5C),
            ("2010.10.26.0000", 19778391, 0x20F94876),
            ("2010.11.25.0002", 250718651, 0x5FBB5B24),
            ("2010.11.30.0000", 6921623, 0xA5479111),
            ("2010.12.06.0000", 7158904, 0xCAD6BC31),
            ("2010.12.13.0000", 263311481, 0xE51EFC06),
            ("2010.12.21.0000", 7521358, 0x93EE1510),
            ("2011.01.18.0000", 9954265, 0x059E8900),
            ("2011.02.01.0000", 11632816, 0x9EE60B39),
            ("2011.02.10.0000", 11714096, 0x0ADE7243),
            ("2011.03.01.0000", 77464101, 0x7818B5BF),
            ("2011.03.24.0000", 108923937, 0xF21852AD),
            ("2011.03.30.0000", 109010880, 0x84CB2682),
            ("2011.04.13.0000", 341603850, 0xFF6C3DB0),
            ("2011.04.21.0000", 343579198, 0x57F4041C),
            ("2011.05.19.0000", 344239925, 0xB16FF18C),
            ("2011.06.10.0000", 344334860, 0xB1CAA88B),
            ("2011.07.20.0000", 584926805, 0x2EA149A9),
            ("2011.07.26.0000", 7649141, 0x5670BA07),
            ("2011.08.05.0000", 152064532, 0x0D9E9FD8),
            ("2011.08.09.0000", 8573687, 0x9B54551A),
            ("2011.08.16.0000", 6118907, 0x75231C57),
            ("2011.10.04.0000", 677633296, 0x95C15318),
            ("2011.10.12.0001", 28941655, 0xB37993E3),
            ("2011.10.27.0000", 29179764, 0x977480DC),
            ("2011.12.14.0000", 374617428, 0xC6FE8FED),
            ("2011.12.23.0000", 22363713, 0x93137C93),
            ("2012.01.18.0000", 48998794, 0x9E55EC7E),
            ("2012.01.24.0000", 49126606, 0x3008D942),
            ("2012.01.31.0000", 49536396, 0x60FDBD0B),
            ("2012.03.07.0000", 320630782, 0x885AD768),
            ("2012.03.09.0000", 8312819, 0xC0040D8C),
            ("2012.03.22.0000", 22027738, 0xEABC501B),
            ("2012.03.29.0000", 8322920, 0x63811C35),
            ("2012.04.04.0000", 8678570, 0xF6E43EEC),
            ("2012.04.23.0001", 289511791, 0x6C3C0201),
            ("2012.05.08.0000", 27266546, 0xB6AABF18),
            ("2012.05.15.0000", 27416023, 0x2D428126),
            ("2012.05.22.0000", 27742726, 0x9163549D),
            ("2012.06.06.0000", 129984024, 0x21DF7238),
            ("2012.06.19.0000", 133434217, 0x8280988A),
            ("2012.06.26.0000", 133581048, 0x4CF33FC8),
            ("2012.07.21.0000", 253224781, 0xA8A42A32),
            ("2012.08.10.0000", 42851112, 0xD8ED4CE3),
            ("2012.09.06.0000", 20566711, 0x4235DF72),
            (ClientVersionInfo.TargetGameVersion, 20874726, 0x8A775526)
        ];

        string fromVersion = ClientVersionInfo.BaseVersion;
        foreach ((string version, long sizeBytes, uint crc32) in gamePatches)
        {
            entries.Add(new PatchEntry(PatchRepository.Game, fromVersion, version, sizeBytes, crc32));
            fromVersion = version;
        }

        return entries;
    }
}

public static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    public static uint Compute(byte[] data)
    {
        using MemoryStream stream = new(data);
        return Compute(stream);
    }

    public static uint ComputeFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Compute(stream);
    }

    public static uint Compute(Stream stream)
    {
        uint crc = 0xFFFFFFFFu;
        byte[] buffer = new byte[64 * 1024];

        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                crc = Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            }
        }

        return ~crc;
    }

    private static uint[] CreateTable()
    {
        uint[] table = new uint[256];

        for (uint i = 0; i < table.Length; i++)
        {
            uint value = i;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) == 1
                    ? 0xEDB88320u ^ (value >> 1)
                    : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }
}
