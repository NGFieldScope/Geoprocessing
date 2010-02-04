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
using System.Runtime.InteropServices;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.DataSourcesGDB;

namespace NatGeo.FieldScope.WatershedTools
{
    public class FlowPath : GPPage
    {
        private static readonly int MAX_STEPS = 65535;
        
        protected void Page_Load (object sender, EventArgs evt) {
            try {
                // Read all our parameters
                double x = Double.Parse(HttpUtility.UrlDecode(Request.Params["x"]));
                double y = Double.Parse(HttpUtility.UrlDecode(Request.Params["y"]));
                
                IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
                string workspacePath = ConfigurationManager.AppSettings["Workspace"];
                IRasterWorkspaceEx workspace = (IRasterWorkspaceEx)workspaceFactory.OpenFromFile(workspacePath, 0);
                
                IRasterDataset[] highResFlowDir = new IRasterDataset[0];
                string hiResDS = ConfigurationManager.AppSettings["HighResolutionFlowDirection"];
                if (hiResDS != null) {
                    string[] hiResDSList = hiResDS.Split(';');
                    if ((hiResDSList.Length > 0) && (hiResDSList[0].Length > 0)) {
                        highResFlowDir = new IRasterDataset[hiResDSList.Length];
                        for (int i = 0; i < hiResDSList.Length; i += 1) {
                            highResFlowDir[i] = workspace.OpenRasterDataset(hiResDSList[i]);
                        }
                    }
                }

                Int32 highResMaxSteps = 100;
                string maxSteps = ConfigurationManager.AppSettings["HighResolutionMaxSteps"];
                if (maxSteps != null) {
                    highResMaxSteps = Int32.Parse(maxSteps);
                }

                string lowResDS = ConfigurationManager.AppSettings["LowResolutionFlowDirection"];
                IRasterDataset lowResFlowDir = workspace.OpenRasterDataset(lowResDS);
                
                // setup output object
                object missing = Type.Missing;
                PathClass path = new PathClass();
                PointClass point = new PointClass();
                point.PutCoords(x, y);
                path.AddPoint(CopyPoint(point), ref missing, ref missing);

                // (possibly) move along high-res flow path until we reach the edge of the watershed
                for (int i = 0; i < highResFlowDir.Length; i += 1) {
                    IRaster hiRes = highResFlowDir[i].CreateDefaultRaster();
                    IRaster2 hiResRaster = hiRes as IRaster2;
                    IRasterBand hiResBand = (hiResRaster as IRasterBandCollection).Item(0);
                    RasterProperties hiResProperties = new RasterProperties(hiResBand);
                    int col;
                    int row;
                    hiResRaster.MapToPixel(x, y, out col, out row);
                    if ((col >= 0) && (col < hiResProperties.Width) && (row >= 0) && (row < hiResProperties.Height)) {
                        object value = hiResRaster.GetPixelValue(0, col, row);
                        if ((value != null) && (!value.Equals(hiResProperties.NoDataValue))) {
                            IRawPixels hiResPixels = hiResBand as IRawPixels;
                            IPnt hiResBlockSize = new Pnt();
                            hiResBlockSize.SetCoords(hiResProperties.Width, hiResProperties.Height);
                            IPixelBlock hiResPB = hiRes.CreatePixelBlock(hiResBlockSize);
                            IPnt hiResOrigin = new Pnt();
                            hiResOrigin.SetCoords(0, 0);
                            hiResPixels.Read(hiResOrigin, hiResPB);
                            System.Array hiResData = (System.Array)(hiResPB as IPixelBlock3).get_PixelDataByRef(0);
                            TracePath(path, point, hiResRaster, hiResProperties, hiResData, highResMaxSteps);
                            Marshal.ReleaseComObject(hiRes);
                            break;
                        }
                    }
                    Marshal.ReleaseComObject(hiRes);
                }

                // Then load and trace the low-resolution flow path
                IRaster loRes = lowResFlowDir.CreateDefaultRaster();
                IRaster2 loResRaster = loRes as IRaster2;
                IRasterBand loResBand = (loResRaster as IRasterBandCollection).Item(0);
                IRawPixels loResPixels = loResBand as IRawPixels;
                RasterProperties loResProperties = new RasterProperties(loResBand);
                IPnt loResBlockSize = new Pnt();
                loResBlockSize.SetCoords(loResProperties.Width, loResProperties.Height);
                IPixelBlock loResPB = loRes.CreatePixelBlock(loResBlockSize);
                IPnt loResOrigin = new Pnt();
                loResOrigin.SetCoords(0, 0);
                loResPixels.Read(loResOrigin, loResPB);
                System.Array loResData = (System.Array)(loResPB as IPixelBlock3).get_PixelDataByRef(0);
                // trace the low-resolution raster until it ends, or until we go MAX_STEPS steps 
                // (to guard against infinite loops in input raster)
                TracePath(path, point, loResRaster, loResProperties, loResData, MAX_STEPS);
                Marshal.ReleaseComObject(loRes);

                Response.ContentType = "text/plain";
                //Response.ContentType = "application/json";
                Response.StatusCode = 200;
                Response.Write("{\n");
                Response.Write("  \"geometryType\":\"esriGeometryPolyline\",\n");
                Response.Write("  \"spatialReference\":{\"wkid\":4326},\n");
                Response.Write("  \"features\":[\n");
                Response.Write("    {\n");
                Response.Write("      \"geometry\":{\n");
                Response.Write("        \"paths\":[\n");
                Response.Write("          [\n");
                for (int i = 0; i < path.PointCount; i += 1) {
                    IPoint p = path.get_Point(i);
                    Response.Write("            [");
                    Response.Write(p.X.ToString("N"));
                    Response.Write(", ");
                    Response.Write(p.Y.ToString("N"));
                    Response.Write("]");
                    if ((i + 1) < path.PointCount) {
                        Response.Write(",");
                    }
                    Response.Write("\n");
                }
                Response.Write("          ]\n");
                Response.Write("        ]\n");
                Response.Write("      },\n");
                Response.Write("      \"attributes\":{\n");
                Response.Write("        \"Shape_Length\":");
                Response.Write(path.Length.ToString("N"));
                Response.Write("\n");
                Response.Write("      }\n");
                Response.Write("    }\n");
                Response.Write("  ]\n");
                Response.Write("}\n");


                foreach (object raster in highResFlowDir) {
                    Marshal.ReleaseComObject(raster);
                }
                Marshal.ReleaseComObject(lowResFlowDir);


            } catch (Exception e) {
                Response.ContentType = "text/plain";
                Response.StatusCode = 500;
                Response.Write(e.ToString());
                Response.Write("\n");
                Response.Write(e.StackTrace);
            }
        }

        private void TracePath(PathClass path,
                                PointClass point,
                                IRaster2 raster,
                                RasterProperties properties,
                                System.Array data,
                                int maxSteps) {
            double dx = 0;
            double dy = 0;
            double lastDx;
            double lastDy;
            object missing = Type.Missing;
            int row;
            int col;
            int steps = 0;
            while (true) {
                steps += 1;
                raster.MapToPixel(point.X, point.Y, out col, out row);
                if ((col < 0) || (col >= properties.Width) || (row < 0) || (row >= properties.Height)) {
                    path.AddPoint(CopyPoint(point), ref missing, ref missing);
                    break;
                }
                object value = data.GetValue(col, row);
                if ((value == null) || (Convert.ToInt32(value) == Convert.ToInt32(properties.NoDataValue))) {
                    path.AddPoint(CopyPoint(point), ref missing, ref missing);
                    break;
                }
                Int32 flowDir = Convert.ToInt32(value);
                lastDx = dx;
                lastDy = dy;
                switch (flowDir) {
                    case 1:
                        dx = properties.CellWidth;
                        dy = 0;
                        break;
                    case 2:
                        dx = properties.CellWidth;
                        dy = -properties.CellHeight;
                        break;
                    case 4:
                        dx = 0;
                        dy = -properties.CellHeight;
                        break;
                    case 8:
                        dx = -properties.CellWidth;
                        dy = -properties.CellHeight;
                        break;
                    case 16:
                        dx = -properties.CellHeight;
                        dy = 0;
                        break;
                    case 32:
                        dx = -properties.CellWidth;
                        dy = properties.CellHeight;
                        break;
                    case 64:
                        dx = 0;
                        dy = properties.CellHeight;
                        break;
                    case 128:
                        dx = properties.CellWidth;
                        dy = properties.CellHeight;
                        break;
                    default:
                        //bool test = (Convert.ToInt32(value) == Convert.ToInt32(properties.NoDataValue));
                        //throw new Exception("invalid cell value " + flowDir);
                        dx = dy = 0;
                        break;
                }
                if ((dx != lastDx) || (dy != lastDy)) {
                    path.AddPoint(CopyPoint(point), ref missing, ref missing);
                }
                if (((dx == 0) && (dy == 0)) || (steps > maxSteps)) {
                    break;
                }
                point.X += dx;
                point.Y += dy;
            }
            path.AddPoint(CopyPoint(point), ref missing, ref missing);
        }
    }
}
