using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.GeoAnalyst;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace GenerateGrid
{
    struct LOD
    {
        public int level;
        public double resolution;
        public double scale;
    }

    struct TileInfo
    {
        public double dpi;
        public int width;
        public int height;
        public double originX;
        public double originY;
        public LOD[] lods;
        public int spatialReference;
    }

    class Program
    {
        static void Main(string[] args) {
            TileInfo tiles = new TileInfo() {
                dpi = 96,
                width = 256,
                height = 256,
                originX = -20037508.342787,
                originY = 20037508.342787,
                lods = new LOD[] {
                    new LOD() { 
                        level = 0,
                        resolution = 156543.033928, 
                        scale = 591657527.591555
                    },
                    new LOD() { 
                        level = 1,
                        resolution = 78271.5169639999, 
                        scale = 295828763.795777
                    },
                    new LOD() { 
                        level = 2,
                        resolution = 39135.7584820001, 
                        scale = 147914381.897889
                    },
                    new LOD() { 
                        level = 3,
                        resolution = 19567.8792409999, 
                        scale = 73957190.948944
                    }
                },
                spatialReference = 102100
            };

            ESRI.ArcGIS.RuntimeManager.Bind(ESRI.ArcGIS.ProductCode.Server);
            IAoInitialize aoInit = new AoInitializeClass();
            aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeArcServer);

            IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
            string workspacePath = "C:\\Users\\Administrator\\Documents\\ArcGIS\\Default.gdb";
            IWorkspace ws = workspaceFactory.OpenFromFile(workspacePath, 0);
            IFeatureWorkspace workspace = ws as IFeatureWorkspace;

            CreateGrid(tiles, 0, workspace, "Tiles_0");
            CreateGrid(tiles, 1, workspace, "Tiles_1");
            CreateGrid(tiles, 2, workspace, "Tiles_2");
            CreateGrid(tiles, 3, workspace, "Tiles_3");
        }

        static void CreateGrid (TileInfo tiles, int level, IFeatureWorkspace destination, String name) {
            ISpatialReferenceFactory2 sEnv = new SpatialReferenceEnvironment() as ISpatialReferenceFactory2;
            ISpatialReference sr = sEnv.CreateSpatialReference((int)tiles.spatialReference);
            sr.SetMDomain(-137434824702, 0);
            IFeatureClass fc = CreateFeatureClass(destination, name, sr);
            LOD lod = tiles.lods[level];
            double width = tiles.width * lod.resolution;
            double height = tiles.height * lod.resolution;
            double y = tiles.originY;
            long row = 0;
            double maxX = -(tiles.originX + width);
            double minY = -(tiles.originY - height);
            while (y > minY)
            {
                double x = tiles.originX;
                long col = 0;
                while (x < maxX)
                {
                    RingClass ring = new RingClass();
                    IPoint tl = new PointClass();
                    tl.PutCoords(x, y);
                    tl.M = -(((col & 0xFFFF) << 16) + (row & 0xFFFF));
                    ring.AddPoint(tl);
                    IPoint tr = new PointClass();
                    tr.PutCoords(x + width, y);
                    tr.M = -((((col + 1) & 0xFFFF) << 16) + (row & 0xFFFF));
                    ring.AddPoint(tr);
                    IPoint br = new PointClass();
                    br.PutCoords(x + width, y - width);
                    br.M = -((((col + 1) & 0xFFFF) << 16) + ((row + 1) & 0xFFFF));
                    ring.AddPoint(br);
                    IPoint bl = new PointClass();
                    bl.PutCoords(x, y - width);
                    bl.M = -(((col & 0xFFFF) << 16) + ((row + 1) & 0xFFFF));
                    ring.AddPoint(bl);

                    ring.AddPoint(tl);

                    ring.Close();
                    PolygonClass polygon = new PolygonClass();
                    polygon.AddGeometry(ring);
                    IFeature polyFeature = fc.CreateFeature();
                    polyFeature.Shape = polygon;
                    polyFeature.Store();
                    x += width;
                    col += 1;
                }
                row += 1;
                y -= height;
            }
            IFeatureClassDescriptor fd = new FeatureClassDescriptorClass();
            fd.Create(fc, null, "OBJECTID");
        }

        static IFeatureClass CreateFeatureClass(IFeatureWorkspace workspace, string name, ISpatialReference outSR)
        {
            IFieldsEdit fields = new FieldsClass();

            IFieldEdit field = new FieldClass();
            field.Type_2 = esriFieldType.esriFieldTypeOID;
            field.Name_2 = "OBJECTID";
            field.AliasName_2 = "OBJECTID";
            fields.AddField(field);

            IGeometryDefEdit geom = new GeometryDefClass();
            geom.GeometryType_2 = esriGeometryType.esriGeometryPolygon;
            geom.SpatialReference_2 = outSR;
            geom.HasM_2 = true;
            geom.HasZ_2 = false;
            
            field = new FieldClass();
            field.Name_2 = "SHAPE";
            field.AliasName_2 = "SHAPE";
            field.Type_2 = esriFieldType.esriFieldTypeGeometry;
            field.GeometryDef_2 = geom;
            fields.AddField(field);

            return workspace.CreateFeatureClass(name, fields, null, null, esriFeatureType.esriFTSimple, "SHAPE", "");
        }
    }
}
