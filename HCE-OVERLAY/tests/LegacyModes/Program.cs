using System;
using System.Collections.Generic;
using HNL.CeilingEstimator.AutoCAD;
using HNL.CeilingEstimator.Core;
using HNL.CeilingEstimator.Core.Models;

internal static class Program
{
    static void Assert(bool ok, string what)
    {
        if (!ok) throw new Exception("HCE MODE FAILED: " + what);
    }

    static void Main()
    {
        int n = 0;
        var engine = new DemtcEngine();
        var rect = new List<Segment2> {
            new Segment2(0, 0, 2400, 0),
            new Segment2(2400, 0, 2400, 2400),
            new Segment2(2400, 2400, 0, 2400),
            new Segment2(0, 2400, 0, 0)
        };
        foreach (var pitch in new[] {600, 610})
        foreach (var mode in new[] {"S", "D", "M"})
        foreach (var direction in new[] {"X", "Y"})
        foreach (var priority in new[] {"S", "D"})
        {
            var profile = new HceLegacyProfile {
                Family=pitch, Module=mode, Direction=direction,
                Priority=priority, SnapEnabled=false, Tolerance=0, GridMode="1"
            };
            var o = profile.ToOptions();
            bool longMain = mode=="D" || mode=="M" && priority=="D";
            var x = longMain && direction=="X" ? 2*pitch : pitch;
            var y = longMain && direction=="Y" ? 2*pitch : pitch;
            Assert(o.GridWidth==x && o.GridHeight==y, "grid "+pitch+"/"+mode+"/"+direction+"/"+priority);
            Assert(o.MixedMode==(mode=="M"), "mixed flag "+mode);
            Assert(o.MixedPrimary==(longMain?MixedPrimaryMode.LargeMain:MixedPrimaryMode.SmallMain), "priority "+mode);
            Assert(mode!="D" || ReferenceEquals(o.SmallStock,o.LargeStock), "long stock when D-only");
            Assert(!o.SnapEnabled && o.SnapTolerance==0, "Snap OFF");
            var calc=engine.CalculateLineHatch(rect,new GridFrame(new Point2(0,0),0),2400d*2400d,o);
            Assert(calc.Success && calc.PureResult!=null, "calc "+pitch+"/"+mode+"/"+direction+"/"+priority+" "+calc.FailureReason);
            var bins=engine.PackCutPieces(calc.PureResult.CutPieces,o);
            Assert(bins!=null,"pack null");
            n++;
            Console.WriteLine("HCE MODE PASS "+n+" "+pitch+"/"+mode+"/"+direction+"/"+priority);
        }
        var defaultProfile=new HceLegacyProfile().ToOptions();
        Assert(defaultProfile.GridWidth==610 && defaultProfile.GridHeight==610 &&
          defaultProfile.MixedMode && defaultProfile.MixedPrimary==MixedPrimaryMode.SmallMain &&
          defaultProfile.SnapEnabled && defaultProfile.SnapTolerance==3,"Golden defaults");
        Console.WriteLine("HCE SIX-MODE CORE GATES PASS "+n+"/24; Golden defaults preserved");
    }
}