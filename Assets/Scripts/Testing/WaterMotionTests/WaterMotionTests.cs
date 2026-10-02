using System;
using NUnit.Framework;
using UnityEngine;
using Checkmate.Water;

public class WaterMotionTests
{
    [Test]
    public void FlowClock_HasBoundedPositiveSpeed()
    {
        for(double t=0;t<300;t+=0.31)
        {
            double speed=(ShallowWaterMotion.FlowClock(t+0.001,32,0.35,2.1)-ShallowWaterMotion.FlowClock(t,32,0.35,2.1))/0.001;
            Assert.That(speed,Is.InRange(0.649,1.351));
        }
    }

    [Test]
    public void FlowClock_DoesNotDependOnStepCount()
    {
        double coarse=0, fine=0;
        for(int i=0;i<30;i++)coarse+=ShallowWaterMotion.FlowClock(i+1,32,0.18,0)-ShallowWaterMotion.FlowClock(i,32,0.18,0);
        for(int i=0;i<1800;i++)fine+=ShallowWaterMotion.FlowClock((i+1)/60.0,32,0.18,0)-ShallowWaterMotion.FlowClock(i/60.0,32,0.18,0);
        Assert.That(coarse,Is.EqualTo(fine).Within(1e-9));
    }

    [Test]
    public void ZeroVariation_MatchesConstantFlow()
    {
        var offset=ShallowWaterMotion.FlowOffset(10,new Vector4(1,1,0,0.025f),32,0,0);
        Assert.That(offset.x,Is.EqualTo(0.25f).Within(1e-6));
        Assert.That(offset.y,Is.EqualTo(0).Within(1e-6));
    }

    [Test]
    public void WrappedFlow_RemainsContinuousOnRepeatTexture()
    {
        var layer=new Vector4(1,1,0,0.1f);
        var a=ShallowWaterMotion.FlowOffset(9.999,layer,32,0,0);
        var b=ShallowWaterMotion.FlowOffset(10.001,layer,32,0,0);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(a.x*360,b.x*360)),Is.LessThan(0.1f));
    }

    [Test]
    public void LongSession_PhaseIsFiniteBoundedAndSmooth()
    {
        double t=60*60*24*30;
        float a=ShallowWaterMotion.WavePhase(t,0.75,0.32,32,0.18);
        float b=ShallowWaterMotion.WavePhase(t+1.0/60,0.75,0.32,32,0.18);
        Assert.That(a,Is.InRange(0,2*(float)Math.PI));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(a*Mathf.Rad2Deg,b*Mathf.Rad2Deg)),Is.LessThan(2));
    }

    [Test]
    public void Disable_PreservesReflectionAndSharedMaterial()
    {
        var go=new GameObject("Motion test",typeof(MeshRenderer));
        var material=new Material(Shader.Find("Checkmate/Water/Shallow Water"));
        try
        {
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            var motion=go.AddComponent<ShallowWaterMotion>();
            var block=new MaterialPropertyBlock();block.SetFloat("_PlanarEnabled",1);block.SetFloat("_MotionEnabled",1);renderer.SetPropertyBlock(block);
            motion.enabled=false;renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_MotionEnabled"),Is.Zero);
            Assert.That(block.GetFloat("_PlanarEnabled"),Is.EqualTo(1));
            Assert.That(material.GetFloat("_MotionEnabled"),Is.Zero);
            Assert.That(renderer.sharedMaterial,Is.SameAs(material));
        }
        finally { UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(material); }
    }
}
