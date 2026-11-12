#if UNITY_ANDROID
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

public class NetworkPolicyTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ResolvesBothUnityCallbackLayoutsAndPreservesHttpsDefault(bool gradleRoot)
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory ?? Environment.CurrentDirectory, "policy-test-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, gradleRoot ? "unityLibrary" : "", "src/main/res/xml/network_sec_config.xml");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, "<network-security-config><base-config cleartextTrafficPermitted=\"false\"/></network-security-config>");
            var method = Type.GetType("FP2NetworkPolicy, Assembly-CSharp-Editor", true).GetMethod("Apply");
            method.Invoke(null, new object[] { root });
            method.Invoke(null, new object[] { root });
            var doc = XDocument.Load(file);
            Assert.That((string)doc.Root.Element("base-config").Attribute("cleartextTrafficPermitted"), Is.EqualTo("false"));
            var hosts = doc.Descendants("domain").Select(x => x.Value).ToArray();
            Assert.That(hosts, Is.EquivalentTo(new[] { "127.0.0.1", "localhost" }));
            Assert.That(doc.Descendants("domain").All(x => (string)x.Attribute("includeSubdomains") == "false"), Is.True);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
#endif
