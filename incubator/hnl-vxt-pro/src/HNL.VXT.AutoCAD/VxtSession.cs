using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.AutoCAD
{
    internal sealed class VxtSession
    {
        public static VxtSession Current { get; } = new VxtSession();

        // V6.7.4 parity: one command can receive a selection set containing many
        // closed ceiling polylines. Each polygon remains an independent ceiling area.
        public List<Boundary2> Boundaries { get; } = new List<Boundary2>();
        public List<ObjectId> BoundaryIds { get; } = new List<ObjectId>();

        // Backward-compatible first boundary for code paths that need one reference
        // point (for example a manual DIM pick). New Preview/Create paths use Boundaries.
        public Boundary2 Boundary
        {
            get => Boundaries.Count == 0 ? null : Boundaries[0];
            set
            {
                Boundaries.Clear();
                BoundaryIds.Clear();
                BoundaryRegionGroups.Clear();
                if (value != null) Boundaries.Add(value);
            }
        }

        public VxtSettings Settings { get; set; } = new VxtSettings();
        public VxtPaletteViewModel ViewModel { get; set; }

        public ObjectId[] GeneralEquipmentIds { get; set; } = new ObjectId[0];
        public ObjectId[] MainEquipmentIds { get; set; } = new ObjectId[0];
        public ObjectId[] FurringEquipmentIds { get; set; } = new ObjectId[0];

        // V6.7.4 ask_each: false = Trái/Dưới, true = Phải/Trên.
        public bool GlobalFurringFromFarEdge { get; set; }

        // Backward-compatible flattened manual regions.
        public List<VxtLayoutRegion> Regions { get; } = new List<VxtLayoutRegion>();

        // Manual rectangle regions owned by each selected ceiling Polyline.
        // Group index == Boundaries/BoundaryIds index.
        public List<List<VxtLayoutRegion>> BoundaryRegionGroups { get; } = new List<List<VxtLayoutRegion>>();

        public bool HasBoundary => Boundaries.Count > 0;
        public bool HasBoundaryRegions => BoundaryRegionGroups.Count > 0;
    }
}
