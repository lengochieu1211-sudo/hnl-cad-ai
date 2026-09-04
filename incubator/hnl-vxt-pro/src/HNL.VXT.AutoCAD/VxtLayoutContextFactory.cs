using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Geometry;
using HNL.VXT.Core.Layout;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtLayoutContextFactory
    {
        public static VxtLayoutContext Build(VxtSession session, Transaction tr)
        {
            var context = new VxtLayoutContext();
            AddExtents(context.GeneralObstacles, session.GeneralEquipmentIds, tr);
            AddExtents(context.MainObstacles, session.MainEquipmentIds, tr);
            AddExtents(context.FurringObstacles, session.FurringEquipmentIds, tr);
            foreach (var region in session.Regions)
                context.Regions.Add(region);
            return context;
        }

        private static void AddExtents(ICollection<Box2> target, IEnumerable<ObjectId> ids, Transaction tr)
        {
            if (ids == null) return;
            foreach (var id in ids)
            {
                if (id.IsNull || id.IsErased || !id.IsValid) continue;
                try
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead, false) as Entity;
                    if (entity == null) continue;
                    var ext = entity.GeometricExtents;
                    target.Add(new Box2(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y));
                }
                catch
                {
                    // Proxy/custom entities without extents are ignored exactly like V6.7.4's
                    // defensive GetBoundingBox behavior.
                }
            }
        }
    }
}
