#if UNITY_ANDROID
using System.IO;
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
        string config = Path.Combine(path, "src/main/res/xml/network_sec_config.xml");
        if (!File.Exists(config)) throw new FileNotFoundException("Unity network security policy missing", config);
        var doc = XDocument.Load(config);
        doc.Root.Add(new XElement("domain-config",new XAttribute("cleartextTrafficPermitted","true"),
            new XElement("domain",new XAttribute("includeSubdomains","false"),"127.0.0.1"),
            new XElement("domain",new XAttribute("includeSubdomains","false"),"localhost")));
        doc.Save(config);
        UnityEngine.Debug.Log("[A3] USB loopback-only network policy applied.");
    }
}
#endif
