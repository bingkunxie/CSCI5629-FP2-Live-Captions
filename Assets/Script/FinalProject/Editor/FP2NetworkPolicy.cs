#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;

// Keep Unity's generated HTTPS-only policy, adding loopback solely for debug USB use.
public sealed class FP2NetworkPolicy : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 1000;
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (!FP2Setup.UsbDevelopmentBuild) return;
        Apply(path);
    }

    public static void Apply(string path)
    {
        const string relative = "src/main/res/xml/network_sec_config.xml";
        // Newer Unity passes the Gradle root; earlier versions pass unityLibrary itself.
        string config = new[] { Path.Combine(path, relative), Path.Combine(path, "unityLibrary", relative) }
            .FirstOrDefault(File.Exists);
        if (config == null) throw new FileNotFoundException("Unity network security policy missing");
        var doc = XDocument.Load(config);
        foreach (string host in new[] { "127.0.0.1", "localhost" })
            if (!doc.Root.Elements("domain-config").Any(e => (string)e.Attribute("cleartextTrafficPermitted") == "true" &&
                    e.Elements("domain").Any(d => d.Value == host)))
                doc.Root.Add(new XElement("domain-config", new XAttribute("cleartextTrafficPermitted", "true"),
                    new XElement("domain", new XAttribute("includeSubdomains", "false"), host)));
        doc.Save(config);
        UnityEngine.Debug.Log("[FP2] USB loopback-only network policy applied.");
    }
}
#endif
