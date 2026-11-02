using System;
using System.IO;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

// Saves what the Scene view camera currently sees to <project>/Screenshots/SceneView_<timestamp>.png.
// Trigger: "Capture" button in the Scene view overlay (enable via the overlay menu, ` key),
// or Tools > Capture Scene View (Ctrl+Shift+F12).
static class SceneViewCapture
{
    const int Width = 1920, Height = 1080; // 16:9

    [MenuItem("Tools/Capture Scene View %#F12")]
    public static void Capture()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null)
        {
            Debug.LogWarning("SceneViewCapture: no Scene view open.");
            return;
        }

        var cam = view.camera;
        int w = Width, h = Height;

        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        float prevAspect = cam.aspect;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        try
        {
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"SceneView_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"SceneViewCapture: saved {path}");
            EditorUtility.RevealInFinder(path);
        }
        finally
        {
            cam.targetTexture = prevTarget;
            cam.aspect = prevAspect;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}

[Overlay(typeof(SceneView), "Capture", true)]
class SceneViewCaptureOverlay : Overlay
{
    public override VisualElement CreatePanelContent() =>
        new Button(SceneViewCapture.Capture) { text = "Capture Scene View" };
}
