using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[Serializable]
[PostProcess(typeof(GaussianBlurRenderer), PostProcessEvent.BeforeStack, "Custom/GaussianBlur")]
public sealed class GaussianBlur : PostProcessEffectSettings
{
    public IntParameter halfKernelSize = new IntParameter() { value = 1 };

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

    int kernelIndexH;
    Vector3 kernelGroupSizesH;

    int kernelIndexV;
    Vector3 kernelGroupSizesV;

    RenderTexture[] targets;

    public GaussianBlurRenderer()
    {
        computeShader = Resources.Load<ComputeShader>("gaussianBlur");

        uint x, y, z;

        kernelIndexH = computeShader.FindKernel("HBlur");
        computeShader.GetKernelThreadGroupSizes(kernelIndexH, out x, out y, out z);
        kernelGroupSizesH = new Vector3(x, y, z);

        kernelIndexV = computeShader.FindKernel("VBlur");
        computeShader.GetKernelThreadGroupSizes(kernelIndexV, out x, out y, out z);
        kernelGroupSizesV = new Vector3(x, y, z);

        targets = new RenderTexture[2];
    }

    void CheckTexture(int id, int width, int height)
    {
        if (targets[id] == null || !targets[id].IsCreated() || targets[id].width != width || targets[id].height != height)
        {
            if (targets[id] != null)
            {
                RuntimeUtilities.Destroy(targets[id]);
            }

            targets[id] = new RenderTexture(width, height, 0);
            targets[id].enableRandomWrite = true;
            targets[id].Create();
        }
    }

    public override void Render(PostProcessRenderContext context)
    {
        var cmd = context.command;
        cmd.BeginSample("Gaussian Blur");

        int kernelGroupNumHorizontal = Mathf.CeilToInt(context.screenHeight / kernelGroupSizesH.x);
        int kernelGroupNumVertical = Mathf.CeilToInt(context.screenWidth / kernelGroupSizesV.x);

        int halfKernelSize = Mathf.Max(1, settings.halfKernelSize.value);
        int kernelSize = halfKernelSize * 2 + 1;

        CheckTexture(0, context.screenWidth, context.screenHeight);
        CheckTexture(1, context.screenWidth, context.screenHeight);

        cmd.SetComputeVectorParam(computeShader, Shader.PropertyToID("_Params1"), new Vector4(context.screenWidth, context.screenHeight, kernelSize, halfKernelSize));

        // first pass
        cmd.SetComputeTextureParam(computeShader, kernelIndexH, Shader.PropertyToID("_Source"), context.source);
        cmd.SetComputeTextureParam(computeShader, kernelIndexH, Shader.PropertyToID("Result"), targets[0]);
        cmd.DispatchCompute(computeShader, kernelIndexH, kernelGroupNumHorizontal, 1, 1);

        cmd.SetComputeTextureParam(computeShader, kernelIndexV, Shader.PropertyToID("_Source"), targets[0]);
        cmd.SetComputeTextureParam(computeShader, kernelIndexV, Shader.PropertyToID("Result"), targets[1]);
        cmd.DispatchCompute(computeShader, kernelIndexV, kernelGroupNumVertical, 1, 1);

        // second pass
        cmd.SetComputeTextureParam(computeShader, kernelIndexH, Shader.PropertyToID("_Source"), targets[1]);
        cmd.SetComputeTextureParam(computeShader, kernelIndexH, Shader.PropertyToID("Result"), targets[0]);
        cmd.DispatchCompute(computeShader, kernelIndexH, kernelGroupNumHorizontal, 1, 1);

        cmd.SetComputeTextureParam(computeShader, kernelIndexV, Shader.PropertyToID("_Source"), targets[0]);
        cmd.SetComputeTextureParam(computeShader, kernelIndexV, Shader.PropertyToID("Result"), targets[1]);
        cmd.DispatchCompute(computeShader, kernelIndexV, kernelGroupNumVertical, 1, 1);

        cmd.BlitFullscreenTriangle(targets[1], context.destination);

        cmd.EndSample("Gaussian Blur");
    }

    public override void Release()
    {
        foreach(var rt in targets)
        {
            RuntimeUtilities.Destroy(rt);
        }
    }
}
