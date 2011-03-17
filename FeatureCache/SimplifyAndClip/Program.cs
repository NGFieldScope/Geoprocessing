using System;
using ESRI.ArcGIS.AnalysisTools;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.CartographyTools;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geoprocessor;


namespace SimplifyAndClip
{
    class Program
    {
        static void Main(string[] args) {

            ESRI.ArcGIS.RuntimeManager.Bind(ESRI.ArcGIS.ProductCode.Server);
            IAoInitialize aoInit = new AoInitializeClass();
            aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeArcServer);

            IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
            string workspacePath = "C:\\Users\\Administrator\\Documents\\ArcGIS\\Default.gdb";
            IWorkspace ws = workspaceFactory.OpenFromFile(workspacePath, 0);
            IFeatureWorkspace workspace = ws as IFeatureWorkspace;
            
            IFeatureClass cells = workspace.OpenFeatureClass("Tiles_1");
            IFeatureClass polygons = workspace.OpenFeatureClass("test");
            double maxOffset = 78271.5169639999 * 1.5;

            SimplifyAndClip(cells, polygons, maxOffset, workspacePath);
        }

        static void SimplifyAndClip (IFeatureClass cells, 
                                     IFeatureClass polygons,
                                     double maxOffset,
                                     string workspacePath) {
            Geoprocessor gp = new Geoprocessor();
            try {
                string simplifiedPolygons = workspacePath + "\\TempSimplify";
                gp.OverwriteOutput = true;
                gp.SetEnvironmentValue("outputMFlag", "Enabled");
                gp.SetEnvironmentValue("MDomain", "-137434824702 0");

                IVariantArray p = new VarArrayClass();
                p.Add(polygons);
                p.Add(simplifiedPolygons);
                p.Add("POINT_REMOVE");
                p.Add(maxOffset);
                p.Add(maxOffset * maxOffset * 0.5);
                p.Add("RESOLVE_ERRORS");
                p.Add("NO_KEEP");
                gp.Execute("SimplifyPolygon_cartography", p, null);
            
                MakeFeatureLayer mf = new MakeFeatureLayer(workspacePath + "\\Tiles_1", "tiles_layer");
                gp.Execute(mf, null);
            
                int nFeatures = cells.FeatureCount(null);
                for (int i = 1; i <= nFeatures; i += 1) {
                    SelectLayerByAttribute sel = new SelectLayerByAttribute("tiles_layer");
                    sel.where_clause = String.Format("OBJECTID = {0}", i);
                    gp.Execute(sel, null);
                
                    p = new VarArrayClass();
                    p.Add(workspacePath + "\\TempSimplify;tiles_layer");
                    p.Add(workspacePath + String.Format("\\tile_{0}", i - 1));
                    gp.Execute("Intersect_analysis", p, null);
                }
            } catch (Exception e) {
                for (int j = 0; j < gp.MessageCount; j += 1) {
                    Console.WriteLine(gp.GetMessage(j));
                }
            }
            Console.WriteLine("finished");
            Console.Read();
        }
    }
}
