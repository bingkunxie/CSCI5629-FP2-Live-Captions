using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CaptionSceneTests
{
    [Test]
    public void DedicatedCaptionSceneHasWiredCaptionViewAndNoChatbot()
    {
        const string path = "Assets/Scenes/FP2LiveCaptions.unity";
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, "The dedicated caption scene must exist.");
        var scene = EditorSceneManager.OpenScene(path);
        var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
        Assert.That(components.Any(c => c.GetType().Name == "A3TalkingCube"), Is.False, "The final project must not run the talking-cube chatbot.");
        var caption = components.Single(c => c.GetType().Name == "QuestLiveCaptions");
        var serialized = new SerializedObject(caption);
        foreach (string field in new[] { "captionText", "statusText", "hintText", "panel" })
            Assert.That(serialized.FindProperty(field)?.objectReferenceValue, Is.Not.Null, field + " must be wired.");
        var status = (Component)serialized.FindProperty("statusText").objectReferenceValue;
        Assert.That(status.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(60), "Error status needs two readable lines.");
        var panel = (RectTransform)serialized.FindProperty("panel").objectReferenceValue;
        Assert.That(panel.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(panel.parent.GetComponent<Camera>(), Is.Not.Null, "The caption panel must follow the headset camera.");
        Assert.That(panel.localPosition.z, Is.InRange(1f, 2f));
        Assert.That(panel.rect.width, Is.GreaterThan(0));
        Assert.That(components.Any(c => c.GetType().Name == "ControllerButtonsMapper"), Is.True);
    }
}
