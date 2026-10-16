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
        int pureRuns = 0;
        int exercisedPureStockBins = 0;
        var engine = new DemtcEngine();
        var pureTotals = new Dictionary<string, (int Full, int Pieces, int Bins, int Oversize, int Purchase)>();
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
            // Single-stock variants must remain a single physical stock family,
            // even if the persisted mixed-priority preference has another value.
            Assert(mode!="S" || o.SmallStock.DisplayName==
                (pitch==600?"595x595":"605x605"), "short-only stock "+pitch+"/"+priority);
            Assert(mode!="D" || o.SmallStock.DisplayName==
                (pitch==600?"595x1195":"605x1210"), "long-only stock "+pitch+"/"+priority);
            // DisplayName is a CAD Table label only: geometry and stock
            // packing must retain their independent nominal 600/610 dimensions.
            Assert(o.SmallStock.DisplayName ==
                (mode=="D"?(pitch==600?"595x1195":"605x1210"):
                           (pitch==600?"595x595":"605x605")),
                "display stock short-slot "+pitch+"/"+mode+"/"+direction);
            Assert(o.LargeStock.DisplayName ==
                (pitch==600?"595x1195":"605x1210"),
                "display long stock "+pitch+"/"+mode+"/"+direction);
            Assert(o.LargeStock.Width==(direction=="Y"?pitch:2*pitch) &&
                   o.LargeStock.Height==(direction=="Y"?2*pitch:pitch),
                "nominal long geometry must not follow 595/605 display strings");
            Assert(o.SmallStock.Width==(mode=="D"?(direction=="Y"?pitch:2*pitch):pitch) &&
                   o.SmallStock.Height==(mode=="D"?(direction=="Y"?2*pitch:pitch):pitch),
                "nominal short-slot geometry must not follow display strings");
            Assert(!o.SnapEnabled && o.SnapTolerance==0, "Snap OFF");
            var calc=engine.CalculateLineHatch(rect,new GridFrame(new Point2(0,0),0),2400d*2400d,o);
            Assert(calc.Success && calc.PureResult!=null, "calc "+pitch+"/"+mode+"/"+direction+"/"+priority+" "+calc.FailureReason);
            var bins=engine.PackCutPieces(calc.PureResult.CutPieces,o);
            Assert(bins!=null,"pack null");
            int full = calc.PureResult.TotalFullCount;
            int cuts = calc.PureResult.CutPieces.Count;
            int packedPieces = bins.Oversize.Count;
            foreach(var bin in bins.Bins) packedPieces += bin.PiecesReversed.Count;
            Assert(packedPieces==cuts,
                "Unaccounted/duplicated cut pieces: "+packedPieces+"/"+cuts+" "+pitch+"/"+mode);
            if(mode=="S" || mode=="D")
            {
                pureRuns++;
                Assert(cuts>0 || pitch==600, "Cut-piece case unexpectedly empty: "+pitch+"/"+mode);
                var expectedStockCode=mode;
                foreach(var bin in bins.Bins)
                {
                    Assert(bin.Code==expectedStockCode,
                        "Unexpected source stock "+bin.Code+" in "+mode+"-only / "+pitch);
                    exercisedPureStockBins++;
                }
                var purchase = checked(full + bins.Bins.Count);
                Assert(purchase>=full, "Negative pure material purchase");
                if (pitch==600)
                {
                    // 2400-square with exact grid phase: 16 short cells
                    // versus 8 long cells. Area/2 must not be disguised
                    // as the same full-panel quantity for both modes.
                    Assert(full==(mode=="S"?16:8),
                        "Pure 600 full count differs from geometric cells: "+
                        full+"/"+mode+"/"+direction);
                }
                var key = pitch+"/"+mode+"/"+direction;
                if (pureTotals.TryGetValue(key, out var previous))
                    Assert(previous==(full,cuts,bins.Bins.Count,bins.Oversize.Count,purchase),
                        "Pure material result changed with unused Mixed Priority: "+key);
                else pureTotals.Add(key,(full,cuts,bins.Bins.Count,bins.Oversize.Count,purchase));
                if(pitch==610)
                    Assert(bins.Bins.Count>0,
                        "Pure stock packing not exercised with 610 cut-cell geometry: "+key);
            }
            n++;
            Console.WriteLine("HCE MODE PASS "+n+" "+pitch+"/"+mode+"/"+direction+"/"+priority);
        }
        var defaultProfile=new HceLegacyProfile().ToOptions();
        Assert(defaultProfile.GridWidth==610 && defaultProfile.GridHeight==610 &&
          defaultProfile.MixedMode && defaultProfile.MixedPrimary==MixedPrimaryMode.SmallMain &&
          defaultProfile.SnapEnabled && defaultProfile.SnapTolerance==3,"Golden defaults");
        Assert(pureRuns==16, "Expected 16 pure S/D executions");
        Assert(exercisedPureStockBins>0, "Pure stock bins were never exercised");
        foreach (var pitch in new[] { 600, 610 })
        foreach (var direction in new[] { "X", "Y" })
        {
            var small = pureTotals[pitch+"/S/"+direction];
            var large = pureTotals[pitch+"/D/"+direction];
            Assert(small.Full>large.Full,
                "S/D full panel quantities unexpectedly identical at "+pitch+"/"+direction);
            Assert(small.Purchase==small.Full+small.Bins &&
                   large.Purchase==large.Full+large.Bins,
                "Pure total purchase must be full plus same-family cut stock");
        }
        Console.WriteLine("HCE LISP PURE STOCK MATERIAL PARITY PASS "+
            pureRuns+"/16; packed bins="+exercisedPureStockBins+
            "; no opposite source, lost pieces, or mixed-priority leakage");
        var db1 = new IntPtr(0x123450);
        var db2 = new IntPtr(0x223450);
        var p1 = HceProfileSessionStore.For(db1);
        p1.Module = "S";
        p1.TableTextHeight = 75;
        Assert(ReferenceEquals(p1, HceProfileSessionStore.For(db1)),
            "Same native DWG identity must retain settings");
        Assert(HceProfileSessionStore.For(db2).Module == "M",
            "Different DWG settings must stay independent");
        HceProfileSessionStore.Queue(db1, p1);
        Assert(HceProfileSessionStore.IsQueued(db1), "Queued settings");
        p1.Module = "D"; p1.TableTextHeight = 100;
        var snapshot = HceProfileSessionStore.Consume(db1, out var fromPalette);
        Assert(fromPalette && snapshot.Module == "S" && snapshot.TableTextHeight == 75,
            "Queued command must use immutable S and 75mm");
        var direct = HceProfileSessionStore.Consume(db1, out fromPalette);
        Assert(!fromPalette && direct.Module == "D" && direct.TableTextHeight == 100,
            "Direct command uses latest DWG settings");
        HceProfileSessionStore.Queue(db1, p1);
        bool wrongDwgBlocked = false;
        try { HceProfileSessionStore.Consume(db2, out fromPalette); }
        catch (InvalidOperationException) { wrongDwgBlocked = true; }
        Assert(wrongDwgBlocked, "Wrong-DWG HCECALC must fail closed");
        HceProfileSessionStore.ClearQueued(db1);
        HceProfileSessionStore.Remove(db1);
        Assert(HceProfileSessionStore.For(db1).Module == "M",
            "Closed DWG clears settings");
        HceProfileSessionStore.Remove(db1);
        HceProfileSessionStore.Remove(db2);
        Console.WriteLine("HCE NATIVE DWG CONFIG SNAPSHOT TESTS PASS");
        Console.WriteLine("HCE SIX-MODE CORE GATES PASS "+n+"/24; Golden defaults preserved");
    }
}