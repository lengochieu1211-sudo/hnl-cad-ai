using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.AutoCAD
{
    internal sealed class VxtSession
    {
        private static readonly VxtSession Instance = new VxtSession();
        private Database _ownerDatabase;

        public static VxtSession Current
        {
            get
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                Instance.EnsureDatabase(doc?.Database);
                return Instance;
            }
        }

        // The palette/settings are application-wide, but every geometry/ObjectId below belongs
        // to exactly one AutoCAD Database. Switching DWG must never reuse ids or geometry from
        // the previous drawing. The DocumentActivated hook calls this eagerly; the Current getter
        // repeats the guard defensively for command/timer paths that may run after a document switch.
        internal static bool SynchronizeDatabase(Database database) => Instance.EnsureDatabase(database);

        internal static bool ReleaseDatabase(Database database)
        {
            if (database != null && !ReferenceEquals(Instance._ownerDatabase, database)) return false;
            if (Instance._ownerDatabase == null && database != null) return false;
            Instance._ownerDatabase = null;
            Instance.ClearDrawingState();
            return true;
        }

        private bool EnsureDatabase(Database database)
        {
            if (ReferenceEquals(_ownerDatabase, database)) return false;
            _ownerDatabase = database;
            ClearDrawingState();
            return true;
        }

        private void ClearDrawingState()
        {
            Boundaries.Clear();
            BoundaryIds.Clear();
            BoundaryRegionGroups.Clear();
            BoundaryFurringFromFarEdges.Clear();
            Regions.Clear();

            GeneralEquipmentIds = new ObjectId[0];
            MainEquipmentIds = new ObjectId[0];
            FurringEquipmentIds = new ObjectId[0];
            ManualMainIds = new ObjectId[0];
            ManualFurringIds = new ObjectId[0];
            ManualHangerIds = new ObjectId[0];

            ManualHangerReverseHorizontal = false;
            ManualHangerReverseVertical = false;
            GlobalFurringFromFarEdge = false;

            // Keep the palette instance and all user settings, but clear drawing-specific UI facts.
            ViewModel?.SetBoundaryStatus("Chưa chọn biên trần.", false);
            ViewModel?.SetPreviewStats(0, 0, 0, 0);
        }

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
                BoundaryFurringFromFarEdges.Clear();
                if (value != null) Boundaries.Add(value);
            }
        }

        public VxtSettings Settings { get; set; } = new VxtSettings();
        public VxtPaletteViewModel ViewModel { get; set; }

        public ObjectId[] GeneralEquipmentIds { get; set; } = new ObjectId[0];
        public ObjectId[] MainEquipmentIds { get; set; } = new ObjectId[0];
        public ObjectId[] FurringEquipmentIds { get; set; } = new ObjectId[0];

        // V6.7.2 legacy manual-source parity. These are populated at Create time when
        // a system is not redrawn but the Lisp would ask the user to select existing CAD
        // members for hanger placement and/or automatic dimensions.
        public ObjectId[] ManualMainIds { get; set; } = new ObjectId[0];
        public ObjectId[] ManualFurringIds { get; set; } = new ObjectId[0];
        public ObjectId[] ManualHangerIds { get; set; } = new ObjectId[0];

        // One-side manual-Ty direction. false = Trái/Dưới; true = Phải/Trên.
        public bool ManualHangerReverseHorizontal { get; set; }
        public bool ManualHangerReverseVertical { get; set; }

        // V6.7.4 ask_each: false = Trái/Dưới, true = Phải/Trên.
        // Global value stays as a fallback for old/single-boundary callers.
        public bool GlobalFurringFromFarEdge { get; set; }

        // Exact ask_each parity for a multi-Polyline selection. Index matches Boundaries/BoundaryIds.
        public List<bool> BoundaryFurringFromFarEdges { get; } = new List<bool>();

        // Backward-compatible flattened manual regions.
        public List<VxtLayoutRegion> Regions { get; } = new List<VxtLayoutRegion>();

        // Manual rectangle regions owned by each selected ceiling Polyline.
        // Group index == Boundaries/BoundaryIds index.
        public List<List<VxtLayoutRegion>> BoundaryRegionGroups { get; } = new List<List<VxtLayoutRegion>>();

        public bool HasBoundary => Boundaries.Count > 0;
        public bool HasBoundaryRegions => BoundaryRegionGroups.Count > 0;
        public bool HasManualMain => ManualMainIds != null && ManualMainIds.Length > 0;
    }
}
