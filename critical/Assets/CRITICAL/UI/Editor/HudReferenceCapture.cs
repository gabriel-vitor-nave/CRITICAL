using System;
using System.IO;
using Critical.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Critical.Editor
{
    // Renders only the game HUD to a texture in the isolated batch project.
    // This never captures a desktop or operates a window/input device.
    public static class HudReferenceCapture
    {
        public static void Capture(string name)
        {
            if (!Application.isBatchMode || !Application.isPlaying)
                throw new InvalidOperationException("HUD export is restricted to the isolated batch Play Mode check.");
            GameHud hud = GameHud.Instance;
            Canvas canvas = hud != null ? hud.GetComponentInChildren<Canvas>() : null;
            if (canvas == null) throw new InvalidOperationException("The run HUD is not available for export.");
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            bool scalerEnabled = scaler != null && scaler.enabled;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousDistance = canvas.planeDistance;
            float previousScale = canvas.scaleFactor;
            RenderTexture previousTarget = RenderTexture.active;
            Transform[] nodes = canvas.GetComponentsInChildren<Transform>(true);
            var layers = new int[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) { layers[i] = nodes[i].gameObject.layer; nodes[i].gameObject.layer = 5; }
            var cameraObject = new GameObject("Batch HUD export camera", typeof(Camera));
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                target.Create();
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 5.4f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(17, 17, 17, 255);
                camera.cullingMask = 1 << 5;
                camera.allowHDR = camera.allowMSAA = false;
                camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                if (scaler != null) scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                canvas.scaleFactor = 1f;
                Canvas.ForceUpdateCanvases();
                ValidateSketchGeometry(canvas, name);
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                else camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                string folder = "Library/CRITICAL/Verification";
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                canvas.scaleFactor = previousScale;
                if (scaler != null) scaler.enabled = scalerEnabled;
                for (int i = 0; i < nodes.Length; i++) if (nodes[i] != null) nodes[i].gameObject.layer = layers[i];
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void ValidateSketchGeometry(Canvas canvas, string name)
        {
            var rows = new System.Text.StringBuilder();
            var required = new System.Collections.Generic.HashSet<string>
                { "ScreenFrame", "HealthPip0", "XPTrack", "WeaponSlot", "QuickSlot1", "DiceSlot" };
            foreach (var graphic in canvas.GetComponentsInChildren<UiSketchGraphic>())
            {
                var renderer = graphic.GetComponent<CanvasRenderer>();
                var mesh = renderer != null ? renderer.GetMesh() : null;
                rows.AppendLine(graphic.name + " rect=" + graphic.rectTransform.rect + " verts=" + (mesh != null ? mesh.vertexCount : 0) +
                    " depth=" + (renderer != null ? renderer.absoluteDepth : -999) + " cull=" + (renderer != null && renderer.cull) +
                    " color=" + graphic.color + " renderColor=" + (renderer != null ? renderer.GetColor().ToString() : "none") +
                    " material=" + graphic.materialForRendering.name + " texture=" + graphic.mainTexture + " canvas=" + graphic.canvas);
                if (required.Remove(graphic.name) && (renderer == null || mesh == null || mesh.vertexCount == 0))
                    throw new InvalidOperationException("Visible HUD geometry is missing: " + graphic.name);
            }
            if (required.Count > 0) throw new InvalidOperationException("Required HUD shapes were not created: " + string.Join(", ", required));
            Directory.CreateDirectory("Library/CRITICAL/Verification");
            File.WriteAllText("Library/CRITICAL/Verification/" + name + "-meshes.txt", rows.ToString());
        }
    }
}
