using System.Xml.Linq;

namespace EchoGate.Core;

public static class ServerXmlWriter
{
    public static XDocument CreateDocument(IEnumerable<ServerProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        XElement root = new("servers");
        foreach (ServerProfile profile in profiles)
        {
            profile.Validate();
            root.Add(new XElement("server",
                new XAttribute("name", profile.Name),
                new XAttribute("host", profile.Host),
                new XAttribute("lobbyPort", profile.LobbyPort),
                new XAttribute("worldPort", profile.WorldPort),
                new XAttribute("mapPort", profile.MapPort)));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    public static string ToXml(IEnumerable<ServerProfile> profiles)
    {
        return CreateDocument(profiles).ToString(SaveOptions.DisableFormatting);
    }

    public static void Write(string path, IEnumerable<ServerProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        CreateDocument(profiles).Save(path);
    }
}
