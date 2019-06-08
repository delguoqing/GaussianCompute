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
        if (targets[id] == null || !targets[id].IsCreated())
        {
            targets[id] = new RenderTexture(width, height, 0);
        }

        if (m_AutoExposurePool[eye][id] == null || !m_AutoExposurePool[eye][id].IsCreated())
        {
            m_AutoExposurePool[eye][id] = new RenderTexture(1, 1, 0, RenderTextureFormat.RFloat) { enableRandomWrite = true };
            m_AutoExposurePool[eye][id].Create();
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

        cmd.SetComputeVectorParam(computeShader, Shader.PropertyToID("_Params1"), new Vector4(context.screenWidth, context.screenHeight, kernelSize, halfKernelSize));

        // first pass
        cmd.DispatchCompute(computeShader, kernelIndexH, kernelGroupNumHorizontal, 1, 1);
        cmd.DispatchCompute(computeShader, kernelIndexV, kernelGroupNumVertical, 1, 1);

        // second pass
        cmd.DispatchCompute(computeShader, kernelIndexH, kernelGroupNumHorizontal, 1, 1);
        cmd.DispatchCompute(computeShader, kernelIndexV, kernelGroupNumVertical, 1, 1);

        cmd.EndSample("Gaussian Blur");
    }

    public override void Release()
    {
    }
}
