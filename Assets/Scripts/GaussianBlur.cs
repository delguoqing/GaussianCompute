using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[Serializable]
[PostProcess(typeof(GaussianBlurRenderer), PostProcessEvent.BeforeStack, "Custom/GaussianBlur")]
public sealed class GaussianBlur : PostProcessEffectSettings
{
    /// <inheritdoc />
    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {
        return enabled.value
            && SystemInfo.supportsComputeShaders
            && !RuntimeUtilities.isAndroidOpenGL
            //&& RenderTextureFormat.RFloat.IsSupported()
            && context.resources.computeShaders.autoExposure
            && context.resources.computeShaders.exposureHistogram;
    }
}

class GaussianBlurRenderer : PostProcessEffectRenderer<GaussianBlur>
{
    ComputeShader computeShader;

    public GaussianBlurRenderer()
    {
        computeShader = Resources.Load<ComputeShader>("gaussianBlur.compute");
    }

    public override void Render(PostProcessRenderContext context)
    {
        var cmd = context.command;
        cmd.BeginSample("Gaussian Blur");


        cmd.EndSample("Gaussian Blur");
    }

    public override void Release()
    {
    }
}
