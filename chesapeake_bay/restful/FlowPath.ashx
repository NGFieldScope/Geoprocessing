<%@ WebHandler Language="C#" Class="FlowPath" %>

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.Configuration;
using System.Configuration;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.DataSourcesGDB;

public class FlowPath : IHttpHandler {
    
    private static readonly string[] EMPTY_LIST = new string[0];

    public FlowPath() {
        IAoInitialize aoInit = new AoInitializeClass();
        aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeArcServer);
    }

    public void ProcessRequest (HttpContext context) {
        try
        {
            double x = Double.Parse(HttpUtility.UrlDecode(context.Request.Params["x"]));
            double y = Double.Parse(HttpUtility.UrlDecode(context.Request.Params["y"]));

            IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
            string workspacePath = ConfigurationSettings.AppSettings["Workspace"];
            IRasterWorkspaceEx rasterWorkspace = (IRasterWorkspaceEx)workspaceFactory.OpenFromFile(workspacePath, 0);

            string lowResDS = ConfigurationSettings.AppSettings["LowResolutionFlowDirection"];
            IRasterDataset loResDataset = rasterWorkspace.OpenRasterDataset(lowResDS);
            
            string[] hiResDSList = EMPTY_LIST;
            string hiResDS = ConfigurationSettings.AppSettings["HighResolutionFlowDirection"];
            if (hiResDS != null) {
                hiResDSList = hiResDS.Split(';');
                if ((hiResDSList.Length == 1) && (hiResDSList[0].Length == 0)) {
                    hiResDSList = EMPTY_LIST;
                }
            }
            Int32 hiResMaxSteps = 100;
            string maxSteps = ConfigurationSettings.AppSettings["HighResolutionMaxSteps"];
            if (maxSteps != null) {
                hiResMaxSteps = Int32.Parse(maxSteps);
            }
            
            // setup output path
            object missing = Type.Missing;
            PathClass path = new PathClass();
            PointClass point = new PointClass();
            point.PutCoords(x, y);
            path.AddPoint((IPoint)point.Clone(), ref missing, ref missing);

            // (possibly) move along high-res flow path until we reach the edge of the watershed
            for (int i = 0; i < hiResDSList.Length; i += 1) {
                IRasterDataset flowDirHiResDS = rasterWorkspace.OpenRasterDataset(hiResDSList[i]);
                if (flowDirHiResDS == null) {
                    throw new Exception("Could not open high resolution flow direction raster " + hiResDSList[i]);
                }
                IRaster hiRes = flowDirHiResDS.CreateDefaultRaster();
                IRaster2 hiResRaster = hiRes as IRaster2;
                IRasterBand hiResBand = (hiResRaster as IRasterBandCollection).Item(0);
                IRasterProps hiResProperties = hiResBand as IRasterProps;
                int col;
                int row;
                hiResRaster.MapToPixel(x, y, out col, out row);
                try {
                    if ((col >= 0) && (col < hiResProperties.Width) && (row >= 0) && (row < hiResProperties.Height)) {
                        object value = hiResRaster.GetPixelValue(0, col, row);
                        if ((value != null) && (!value.Equals(hiResProperties.NoDataValue))) {
                            IRawPixels hiResPixels = hiResBand as IRawPixels;
                            IPnt hiResBlockSize = new PntClass();
                            hiResBlockSize.SetCoords(hiResProperties.Width, hiResProperties.Height);
                            IPixelBlock hiResPB = hiRes.CreatePixelBlock(hiResBlockSize);
                            IPnt hiResOrigin = new PntClass();
                            hiResOrigin.SetCoords(0, 0);
                            hiResPixels.Read(hiResOrigin, hiResPB);
                            System.Array hiResData = (System.Array)(hiResPB as IPixelBlock3).get_PixelDataByRef(0);
                            TracePath(path, point, hiResRaster, hiResProperties, hiResData, hiResMaxSteps);
                            break;
                        }
                    }
                } catch (OutOfMemoryException e) {
                    throw new Exception("Not enough memory to load high-resolution raster " + hiResDSList[i], e);
                } finally {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(flowDirHiResDS);
                }
            }

            // Then load and trace the low-resolution flow path

            IRaster loRes = loResDataset.CreateDefaultRaster();
            IRaster2 loResRaster = loRes as IRaster2;
            IRasterBand loResBand = (loResRaster as IRasterBandCollection).Item(0);
            IRawPixels loResPixels = loResBand as IRawPixels;
            IRasterProps loResProperties = loResBand as IRasterProps;
            IPnt loResBlockSize = new PntClass();
            loResBlockSize.SetCoords(loResProperties.Width, loResProperties.Height);
            IPixelBlock loResPB = loRes.CreatePixelBlock(loResBlockSize);
            IPnt loResOrigin = new PntClass();
            loResOrigin.SetCoords(0, 0);
            loResPixels.Read(loResOrigin, loResPB);
            try {
                System.Array loResData = (System.Array)(loResPB as IPixelBlock3).get_PixelDataByRef(0);
                // trace the low-resolution raster until it ends, or until we go 65535 steps 
                // (to guard against infinite loops in input raster)
                TracePath(path, point, loResRaster, loResProperties, loResData, 65535);
            } catch (OutOfMemoryException e) {
                throw new Exception("Not enough memory to load low-resolution raster " + loResDataset.ToString(), e);
            } finally {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(loRes);
            }

            PolylineClass polyLine = new PolylineClass();
            polyLine.AddGeometry(path, ref missing, ref missing);


            context.Response.ContentType = "text/plain";
            //context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            context.Response.Write("{\n");
            context.Response.Write("  \"results\":[\n");
            context.Response.Write("    {\n");
            context.Response.Write("      \"paramName\":\"flowpath\",\n");
            context.Response.Write("      \"dataType\":\"GPFeatureRecordSetLayer\",\n");
            context.Response.Write("      \"value\":{\n");
            context.Response.Write("        \"geometryType\":\"esriGeometryPolyline\",\n");
            context.Response.Write("        \"spatialReference\":{\"wkid\":4326},\n");
            context.Response.Write("        \"features\":[\n");
            context.Response.Write("          {\n");
            context.Response.Write("            \"geometry\":{\n");
            context.Response.Write("              \"paths\":[\n");
            context.Response.Write("                [\n");
            for (int i = 0; i < path.PointCount; i += 1) {
                IPoint p = path.get_Point(i);
                context.Response.Write("                  [");
                context.Response.Write(p.X.ToString());
                context.Response.Write(", ");
                context.Response.Write(p.Y.ToString());
                context.Response.Write("]");
                if ((i + 1) < path.PointCount) {
                    context.Response.Write(",");
                }
                context.Response.Write("\n");
            }
            context.Response.Write("                ]\n");
            context.Response.Write("              ]\n");
            context.Response.Write("            },\n");
            context.Response.Write("            \"attributes\":{\n");
            context.Response.Write("              \"Shape_Length\":");
            context.Response.Write(path.Length.ToString());
            context.Response.Write("\n");
            context.Response.Write("            }\n");
            context.Response.Write("          }\n");
            context.Response.Write("        ]\n");
            context.Response.Write("      }\n");
            context.Response.Write("    }\n");
            context.Response.Write("  ],\n");
            context.Response.Write("  \"messages\":[]\n");
            context.Response.Write("}\n");
        } catch (Exception e) {
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 500;
            context.Response.Write(e.ToString());
            context.Response.Write("\n");
            context.Response.Write(e.StackTrace);
        }
    }
  
    public bool IsReusable {
        get {
          return true;
        }
    }
    
    private void TracePath (PathClass path, 
                            PointClass point, 
                            IRaster2 raster, 
                            IRasterProps properties, 
                            System.Array data,
                            int maxSteps) {
        double cellWidth = properties.MeanCellSize().X;
        double cellHeight = properties.MeanCellSize().Y;
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
                path.AddPoint((IPoint)point.Clone(), ref missing, ref missing);
                break;
            }
            object value = data.GetValue(col, row);
            if ((value == null) || (Convert.ToInt32(value) == Convert.ToInt32(properties.NoDataValue))) {
                path.AddPoint((IPoint)point.Clone(), ref missing, ref missing);
                break;
            }
            Int32 flowDir = Convert.ToInt32(value);
            lastDx = dx;
            lastDy = dy;
            switch (flowDir) {
                case 1:
                    dx = cellWidth;
                    dy = 0;
                    break;
                case 2:
                    dx = cellWidth;
                    dy = -cellHeight;
                    break;
                case 4:
                    dx = 0;
                    dy = -cellHeight;
                    break;
                case 8:
                    dx = -cellWidth;
                    dy = -cellHeight;
                    break;
                case 16:
                    dx = -cellWidth;
                    dy = 0;
                    break;
                case 32:
                    dx = -cellWidth;
                    dy = cellHeight;
                    break;
                case 64:
                    dx = 0;
                    dy = cellHeight;
                    break;
                case 128:
                    dx = cellWidth;
                    dy = cellHeight;
                    break;
                default:
                    bool test = (Convert.ToInt32(value) == Convert.ToInt32(properties.NoDataValue));
                    //throw new Exception("invalid cell value " + flowDir);
                    dx = dy = 0;
                    break;
            }
            if ((dx != lastDx) || (dy != lastDy)) {
                path.AddPoint((IPoint)point.Clone(), ref missing, ref missing);
            } 
            if (((dx == 0) && (dy == 0)) || (steps > maxSteps)) {
                break;
            }
            point.X += dx;
            point.Y += dy;
        }
        path.AddPoint((IPoint)point.Clone(), ref missing, ref missing);
    }
}
