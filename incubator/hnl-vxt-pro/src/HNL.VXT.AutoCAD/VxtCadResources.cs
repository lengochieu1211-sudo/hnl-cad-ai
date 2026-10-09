using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HNL.VXT.Core.Models;

namespace HNL.VXT.AutoCAD
{
    internal static class VxtCadResources
    {
        public static void EnsureAll(Database db, Transaction tr, VxtSettings s)
        {
            if (s.DrawMain)
            {
                EnsureLinetype(db, s.MainLinetype);
                EnsureLayer(db, tr, s.MainLayer, s.MainColorIndex, s.MainLinetype, s.MainLineweight);
            }

            if (s.DrawFurring)
            {
                EnsureLinetype(db, s.FurringLinetype);
                EnsureLayer(db, tr, s.FurringLayer, s.FurringColorIndex, s.FurringLinetype, s.FurringLineweight);
            }

            if (s.DrawHangers)
            {
                EnsureLinetype(db, s.HangerLinetype);
                EnsureLayer(db, tr, s.HangerLayer, s.HangerColorIndex, s.HangerLinetype, s.HangerLineweight);
            }

            if (NeedsDimensionResources(s))
            {
                EnsureLinetype(db, s.DimensionLinetype);
                EnsureLayer(db, tr, s.DimensionLayer, s.DimensionColorIndex, s.DimensionLinetype, s.DimensionLineweight);
            }
        }

        public static bool NeedsDimensionResources(VxtSettings s)
            => s != null && s.AutoDimension && (s.DimMain || s.DimFurring || s.DimHanger);

        private static void EnsureLinetype(Database db, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "Continuous", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase)) return;
            try { db.LoadLineTypeFile(name, "acadiso.lin"); }
            catch
            {
                try { db.LoadLineTypeFile(name, "acad.lin"); }
                catch { }
            }
        }

        public static ObjectId EnsureLayer(Database db, Transaction tr, string name, short colorIndex, string linetype, string lineweightText)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Tên Layer không được để trống.");
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            LayerTableRecord record;
            ObjectId id;

            if (layers.Has(name))
            {
                id = layers[name];
                record = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
            }
            else
            {
                layers.UpgradeOpen();
                record = new LayerTableRecord { Name = name };
                id = layers.Add(record);
                tr.AddNewlyCreatedDBObject(record, true);
            }

            if (colorIndex >= 1 && colorIndex <= 255)
                record.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);

            var ltypes = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (!string.IsNullOrWhiteSpace(linetype) && ltypes.Has(linetype))
                record.LinetypeObjectId = ltypes[linetype];

            short lw;
            if (short.TryParse(lineweightText, out lw))
            {
                try { record.LineWeight = (LineWeight)lw; } catch { }
            }
            return id;
        }

        public static void ApplyByLayer(Entity entity, ObjectId layerId)
        {
            entity.LayerId = layerId;
            entity.Color = Color.FromColorIndex(ColorMethod.ByAci, 256);
            entity.LineWeight = LineWeight.ByLayer;
        }
    }
}
