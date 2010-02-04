using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Services;
using System.Web.Configuration;
using System.Web.Caching;
using System.Configuration;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.GeoAnalyst;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.SpatialAnalyst;

namespace NatGeo.FieldScope.WatershedTools
{
    public class QueryPolygon : GPPage
    {
        public QueryPolygon ()
            : base(esriLicenseExtensionCode.esriLicenseExtensionCodeSpatialAnalyst) { }
        
        protected void Page_Load (object sender, EventArgs evt) {
            try {
                IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
                string workspacePath = ConfigurationManager.AppSettings["Workspace"];
                IRasterWorkspaceEx workspace = (IRasterWorkspaceEx)workspaceFactory.OpenFromFile(workspacePath, 0);

                IWorkspace scratchWorkspace;
                IScratchWorkspaceFactory2 scratchFactory = new ScratchWorkspaceFactoryClass();
                if (scratchFactory.CurrentScratchWorkspace != null) {
                    scratchWorkspace = scratchFactory.CurrentScratchWorkspace;
                } else if (scratchFactory.DefaultScratchWorkspace != null) {
                    scratchWorkspace = scratchFactory.DefaultScratchWorkspace;
                } else {
                    scratchWorkspace = scratchFactory.CreateNewScratchWorkspace();
                }
                
                RingClass ring = new RingClass();
                object missing = Type.Missing;
                foreach (string pstring in Context.Request.Params["polygon"].Split(',')) {
                    string[] coords = pstring.Split(' ');
                    IPoint point = new PointClass();
                    point.PutCoords(Double.Parse(coords[0]), Double.Parse(coords[1]));
                    ring.AddPoint(point, ref missing, ref missing);
                }
                PolygonClass polygon = new PolygonClass();
                polygon.AddGeometry(ring, ref missing, ref missing);
                IZonalOp tabulateOp = new RasterZonalOp();
                IExtractionOp extractOp = new RasterExtractionOpClass();

                Dictionary<string, Dictionary<string, Double>> results =
                    new Dictionary<string, Dictionary<string, Double>>();
                
                foreach (string layer in Context.Request.Params["layers"].Split(',')) {
                    Dictionary<string, Double> layerStats = new Dictionary<string, double>();
                    IRasterDataset layerDS = workspace.OpenRasterDataset(layer);
                    IRaster layerRaster = layerDS.CreateDefaultRaster();
                    IRasterBand layerBand = (layerRaster as IRasterBandCollection).Item(0);
                    if (layerBand.AttributeTable != null) {

                        ITable tabs = tabulateOp.TabulateArea(polygon, layerRaster);


                    } else {
                        IRaster extract = extractOp.Polygon(layerDS as IGeoDataset, polygon, true) as IRaster;
                        IRasterBand extractBand = (extract as IRasterBandCollection).Item(0);
                        IRasterStatistics extractStats = extractBand.Statistics;
                        extractStats.Recalculate();
                        layerStats.Add("MIN", extractStats.Minimum);
                        layerStats.Add("MAX", extractStats.Maximum);
                        layerStats.Add("MEAN", extractStats.Mean);
                        layerStats.Add("STDEV", extractStats.StandardDeviation);
                        results.Add(layer, layerStats);
                    }
                }








            } catch (Exception e) {
                Response.ContentType = "text/plain";
                Response.StatusCode = 500;
                Response.Write("ERROR\n");
                Response.Write(e.ToString());
                Response.Write("\n");
                Response.Write(e.StackTrace);
            }
        }
    }
}
