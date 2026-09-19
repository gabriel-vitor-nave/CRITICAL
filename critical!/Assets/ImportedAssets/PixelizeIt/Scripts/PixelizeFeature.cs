

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelizeFeature : ScriptableRendererFeature
{
    // PixelizeIt requires the Universal Render Pipeline package.
    [System.Serializable]
    public class CustomPassSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public int screenHeight = 144;
    }

    [SerializeField] private CustomPassSettings settings = new CustomPassSettings();
    private PixelizePass customPass;

    public override void Create()
    {
        if (settings == null)
            settings = new CustomPassSettings();

        customPass = new PixelizePass(settings);
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {

#if UNITY_EDITOR
        if (renderingData.cameraData.isSceneViewCamera) return;
#endif
        if (customPass != null)
            renderer.EnqueuePass(customPass);
    }
}


