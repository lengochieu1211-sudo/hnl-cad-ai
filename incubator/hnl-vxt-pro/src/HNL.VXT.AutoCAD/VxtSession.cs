using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Models;
using HNL.VXT.UI.ViewModels;

namespace HNL.VXT.AutoCAD
{
    internal sealed class VxtSession
    {
        public static VxtSession Current { get; } = new VxtSession();

        public Boundary2 Boundary { get; set; }
        public VxtSettings Settings { get; set; } = new VxtSettings();
        public VxtPaletteViewModel ViewModel { get; set; }

        public ObjectId[] GeneralEquipmentIds { get; set; } = new ObjectId[0];
        public ObjectId[] MainEquipmentIds { get; set; } = new ObjectId[0];
        public ObjectId[] FurringEquipmentIds { get; set; } = new ObjectId[0];

        public bool HasBoundary => Boundary != null;
    }
}
