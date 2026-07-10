using Emgu.CV;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using UVtools.Core;
using UVtools.Core.Extensions;
using UVtools.Core.Layers;
using UVtools.Core.Scripting;
using Range = Emgu.CV.Structure.Range;

namespace UVtools.ScriptSample;

public class ScriptGaussianLayerMerger : ScriptGlobals
{
    readonly ScriptNumericalInput<ushort> SublayerCount = new()
    {
        Label = "Number of sub-layers that compose one full layer (e.g., four 10um layers make one 40um layer)",
        Unit = "layers",
        Minimum = 2,
        Maximum = 10,
        Increment = 1,
        Value = 4,
    };

    readonly ScriptNumericalInput<double> LayerDepth = new()
    {
        Label = "Sampling depth (how many layers beneath the top layer are sampled when merging)",
        Unit = "layers",
        Minimum = 1.0,
        Maximum = 10.0,
        Increment = 0.1,
        Value = 2.5,
    };

    readonly ScriptNumericalInput<double> Dimming = new()
    {
        Label = "Dimming (applied to layers below the top)",
        Unit = "percent",
        Minimum = 0.1,
        Maximum = 1.0,
        Increment = 0.01,
        Value = 0.75,
    };

    readonly ScriptNumericalInput<double> GreyOffset = new()
    {
        Label = "Grey offset (AKA black level)",
        Unit = "percent",
        Minimum = 0.0,
        Maximum = 1.0,
        Increment = 0.01,
        Value = 0.04,
    };
    
    public void ScriptInit()
    {
        Script.Name = "Gaussian Layer Merger";
        Script.Description = "Merges multiple sub-layers into a single printable layer (e.g., slice at 10um and print at 40um while preserving 10um detail)";
        Script.Author = "Łukasz Łazarecki";
        Script.Version = new Version(1, 3);
        Script.UserInputs.Add(SublayerCount);
        Script.UserInputs.Add(LayerDepth);
        Script.UserInputs.Add(Dimming);
        Script.UserInputs.Add(GreyOffset);
    }

    public string? ScriptValidate()
    {
        return SlicerFile.LayerCount < Math.Ceiling(LayerDepth.Value) 
            ? $"This script requires at least {LayerDepth.Value} layers in order to run." 
            : null;
    }
    
    public bool ScriptExecute()
    {
        Progress.Reset("Merging layers", Operation.LayerRangeCount); // Sets the progress name and the number of items to process

        int layerDepth = (int) Math.Ceiling(LayerDepth.Value);
        int sublayerCount = SublayerCount.Value;

        var gaussianStepCount = layerDepth * sublayerCount;
        var gaussianFactors = new List<double>(gaussianStepCount);
        var sigma = Math.Max(0.0, LayerDepth.Value);

        // calculate gaussian factors, used for layer merging
        for (int i = 0; i < gaussianStepCount; i++)
            gaussianFactors.Add(CalculateGaussianFactor((double) i / sublayerCount, sigma));

        var allLayers = SlicerFile.Layers.ToList();
        
        int totalFullLayerCount = allLayers.Count / sublayerCount;
        int emptySublayerCount  = sublayerCount - (allLayers.Count - totalFullLayerCount * sublayerCount) + layerDepth * sublayerCount;

        // add missing empty layers on top of existing layers
        for (int i = 0; i < emptySublayerCount; i++)
        {
            using var mat = EmguExtensions.InitMat(SlicerFile.Resolution);
            mat.SetTo(new MCvScalar(0.0));
            
            var emptyLayer = allLayers.Last().Clone();
            emptyLayer.LayerMat = mat;
            
            allLayers.Add(emptyLayer);
        }

        var cachedMats = new List<Mat>();

        for (int i = allLayers.Count - 1; i >= 0; --i)
        {
            if(Progress.Token.IsCancellationRequested)
                break;
            
            Progress.PauseIfRequested();
            
            ProcessLayerIfEligible(allLayers, cachedMats, i, sublayerCount, gaussianFactors, Dimming.Value, GreyOffset.Value);
            RemoveLayerIfEligible(allLayers, i, sublayerCount);

            Progress.LockAndIncrement();
        }

        foreach (var mat in cachedMats)
        {
            mat.Dispose();
        }

        if(Progress.Token.IsCancellationRequested)
            return false;
        
        SlicerFile.SuppressRebuildPropertiesWork(() =>
        {
            SlicerFile.LayerHeight *= sublayerCount;
            SlicerFile.BottomLayersHeight *= sublayerCount;
            SlicerFile.Layers = allLayers.ToArray();
        }, true, true);
        
        return true;
    }
    
    private double CalculateGaussianFactor(double x, double sigma) 
    {
        return Math.Exp(-(x * x) / (2.0 * sigma * sigma)) / (Math.Sqrt(2.0 * Math.PI) * sigma);
    }

    private void ProcessLayerIfEligible(List<Layer> layers, List<Mat> cachedMats, int layerIndex, int sublayerCount, List<double> gaussianFactors, double dimming, double greyOffset)
    {
        // process only the top layer of each sublayer stack
        if(layerIndex % sublayerCount != sublayerCount - 1)
            return;
        
        using var mat = layers[layerIndex].LayerMat;
        var original = mat.Clone();     // Keep a original mat copy

        using var mergedTarget = MergeSublayers(layers, cachedMats, layerIndex, sublayerCount, gaussianFactors, dimming, greyOffset);
        using var target = Operation.GetRoiOrDefault(mat);
        mergedTarget.ConvertTo(target, target.Depth);

        Operation.ApplyMask(original, target);
        
        layers[layerIndex].LayerMat = mat;
    }

    private void RemoveLayerIfEligible(List<Layer> layers, int layerIndex, int sublayerCount)
    {
        // don't remove process the top layer of each sublayer stack
        if(layerIndex % sublayerCount == sublayerCount - 1)
            return;
        
        layers.RemoveAt(layerIndex);
    }
    
    // this can only be called internally and in the right order - from the top layer to the bottom, only for eligible layers in between
    private Mat MergeSublayers(List<Layer> layers, List<Mat> cachedMats, int topLayerIndex, int sublayerCount, List<double> gaussianFactors, double dimming, double greyOffset)
    {
        Mat? merged = null;
        var totalWeight = 0.0;

        // remove sublayers for the previous top layer
        if (cachedMats.Count >= sublayerCount)
        {
            for (int i = 0; i < sublayerCount; i++)
                cachedMats[i].Dispose();
            
            cachedMats.RemoveRange(0, sublayerCount);
        }
        
        Mat baseFloatMat = null;
        for (int i = 0; i < gaussianFactors.Count; ++i)
        {
            var gaussianFactor = gaussianFactors[i];
            
            // the last bottom sublayers are sometimes exported blank, don't use them
            int layerIndex = Math.Max(sublayerCount, topLayerIndex - i);
            int cachedMatIndex = Math.Max(0, topLayerIndex - layerIndex);
            
            var cachedMat = cachedMatIndex < cachedMats.Count ? cachedMats[cachedMatIndex] : null;

            Mat floatMat;
            if (cachedMat == null)
            {
                using var mat = layers[layerIndex].LayerMat;
                using var target = Operation.GetRoiOrDefault(mat);

                floatMat = new Mat();
                target.ConvertTo(floatMat, DepthType.Cv32F);

                cachedMats.Add(floatMat);
            }
            else
            {
                floatMat = cachedMat;
            }

            if (i == 0)
                baseFloatMat = floatMat;

            merged ??= EmguExtensions.InitMat(floatMat.Size, new MCvScalar(0.0), 1, DepthType.Cv32F);
            
            CvInvoke.ScaleAdd(floatMat, gaussianFactor, merged, merged);
            totalWeight += gaussianFactor;
        }
        
        merged *= (1 / totalWeight) * dimming;

        CvInvoke.Max(baseFloatMat, merged, merged);

        ApplyGreyOffset(merged, greyOffset);

        return merged;
    }

    private void ApplyGreyOffset(Mat mat, double greyOffset)
    {
        if(greyOffset <= 0.0)
            return;

        var lowEnd = Math.Max(1.0, 255.0 * greyOffset);
        var scale = (255.0 - lowEnd) / 254.0;
        var shift = lowEnd - scale;

        using var mask = new Mat();
        using var remapped = new Mat();

        CvInvoke.Compare(mat, new ScalarArray(new MCvScalar(0.0)), mask, CmpType.GreaterThan);
        mat.ConvertTo(remapped, mat.Depth, scale, shift);
        remapped.CopyTo(mat, mask);
    }
}
