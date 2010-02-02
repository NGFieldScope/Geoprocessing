<%@ WebHandler Language="C#" Class="NatGeo.FieldScope.WatershedTools.UpstreamArea" %>

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.Configuration;
using System.Web.Caching;
using System.Configuration;
using System.Collections.Generic;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.GeoAnalyst;

namespace NatGeo.FieldScope.WatershedTools
{
    public class UpstreamArea : IHttpHandler
    {
        private static void testPoint(System.Drawing.Point pt,
                                      Int32 inflowDirection,
                                      System.Array flowDirData,
                                      System.Array outData,
                                      HashSet<System.Drawing.Point> visited,
                                      Queue<System.Drawing.Point> queue) {
            if ((!visited.Contains(pt)) && (pt.X >= 0) && (pt.X < flowDirData.GetLength(0)) && (pt.Y >= 0) && (pt.Y < flowDirData.GetLength(1))) {
                object value = flowDirData.GetValue(pt.X, pt.Y);
                if ((value != null) && (Convert.ToInt32(value) == inflowDirection)) {
                    queue.Enqueue(pt);
                    visited.Add(pt);
                }
            }
        }

        private IWorkspace m_scratchWorkspace;
        private IRasterDataset m_flowAccum;
        private Int32 m_highResAccumThreshold;
        private IRasterDataset[] m_highResFlowDir;
        private IRasterDataset m_lowResFlowDir;
        private IRasterDataset m_flowArea;
        private IFeatureClass m_flowLine;
        
        public bool IsReusable {
            get {
              return true;
            }
        }

        public UpstreamArea() {
            IAoInitialize aoInit = new AoInitializeClass();
            aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeArcServer);
            aoInit.CheckOutExtension(esriLicenseExtensionCode.esriLicenseExtensionCodeSpatialAnalyst);
            
            IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
            string workspacePath = ConfigurationSettings.AppSettings["Workspace"];
            IWorkspace workspace = workspaceFactory.OpenFromFile(workspacePath, 0);
            IRasterWorkspaceEx rasterWorkspace = (IRasterWorkspaceEx)workspace;

            IScratchWorkspaceFactory  scratchFactory = new FileGDBScratchWorkspaceFactoryClass();
            m_scratchWorkspace = scratchFactory.CreateNewScratchWorkspace();
            
            string flowAccumDS = ConfigurationSettings.AppSettings["FlowAccumulation"];
            m_flowAccum = rasterWorkspace.OpenRasterDataset(flowAccumDS);
            
            m_highResFlowDir = new IRasterDataset[0];
            string hiResDS = ConfigurationSettings.AppSettings["HighResolutionFlowDirection"];
            if (hiResDS != null) {
                string[] hiResDSList = hiResDS.Split(';');
                if ((hiResDSList.Length > 0) && (hiResDSList[0].Length > 0)) {
                    m_highResFlowDir = new IRasterDataset[hiResDSList.Length];
                    for (int i = 0; i < hiResDSList.Length; i += 1) {
                        m_highResFlowDir[i] = rasterWorkspace.OpenRasterDataset(hiResDSList[i]);
                    }
                }
            }
            
            m_highResAccumThreshold = 20;
            string threshold = ConfigurationSettings.AppSettings["HighResolutionAccumThreshold"];
            if (threshold != null) {
                m_highResAccumThreshold = Int32.Parse(threshold);
            }
            
            string lowResDS = ConfigurationSettings.AppSettings["LowResolutionFlowDirection"];
            m_lowResFlowDir = rasterWorkspace.OpenRasterDataset(lowResDS);
            
            string flowAreaDS = ConfigurationSettings.AppSettings["FlowArea"];
            m_flowArea = rasterWorkspace.OpenRasterDataset(flowAreaDS);
            
            string flowLineDS = ConfigurationSettings.AppSettings["FlowLine"];
            m_flowLine = ((IFeatureWorkspace)workspace).OpenFeatureClass(flowLineDS);
        }

        public void ProcessRequest (HttpContext context) {
            try {
                double x = Double.Parse(HttpUtility.UrlDecode(context.Request.Params["x"]));
                double y = Double.Parse(HttpUtility.UrlDecode(context.Request.Params["y"]));
                
                Queue<System.Drawing.Point> queue = new Queue<System.Drawing.Point>();
                HashSet<System.Drawing.Point> visited = new HashSet<System.Drawing.Point>();
                
                IRaster flowAccum = m_flowAccum.CreateDefaultRaster();
                IRaster2 flowAccumRaster = flowAccum as IRaster2;
                int col, row;
                flowAccumRaster.MapToPixel(x, y, out col, out row);
                Int32 accum = Convert.ToInt32(flowAccumRaster.GetPixelValue(0, col, row));
                
                IRaster2 flowDirectionRaster = null;
                IRasterProps flowDirProperties = null;
                System.Array flowDirData = null;
                IPnt flowDirBlockSize = new PntClass();

                if (accum <= m_highResAccumThreshold) {
                    for (int i = 0; i < m_highResFlowDir.Length; i += 1) {
                        IRaster flowDirection = m_highResFlowDir[i].CreateDefaultRaster();
                        flowDirectionRaster = flowDirection as IRaster2;
                        IRasterBand flowDirectionBand = (flowDirectionRaster as IRasterBandCollection).Item(0);
                        flowDirProperties = flowDirectionBand as IRasterProps;
                        flowDirectionRaster.MapToPixel(x, y, out col, out row);
                        if ((col >= 0) && (col < flowDirProperties.Width) && (row >= 0) && (row < flowDirProperties.Height)) {
                            object value = flowDirectionRaster.GetPixelValue(0, col, row);
                            if ((value != null) && (!value.Equals(flowDirProperties.NoDataValue))) {
                                // use high-res raster
                                IRawPixels flowDirectionPixels = flowDirectionBand as IRawPixels;
                                flowDirBlockSize.SetCoords(flowDirProperties.Width, flowDirProperties.Height);
                                IPixelBlock inPB = flowDirection.CreatePixelBlock(flowDirBlockSize);
                                IPnt pixelOrigin = new PntClass();
                                pixelOrigin.SetCoords(0, 0);
                                flowDirectionPixels.Read(pixelOrigin, inPB);
                                flowDirData = (System.Array)(inPB as IPixelBlock3).get_PixelDataByRef(0);
                                break;
                            }
                        }
                    }
                }

                if (flowDirData == null) {
                    // Get low resolution flow direction raster
                    IRaster flowDirection = m_lowResFlowDir.CreateDefaultRaster();
                    flowDirectionRaster = flowDirection as IRaster2;
                    IRasterBand flowDirectionBand = (flowDirectionRaster as IRasterBandCollection).Item(0);
                    IRawPixels flowDirectionPixels = flowDirectionBand as IRawPixels;
                    flowDirProperties = flowDirectionBand as IRasterProps;
                    flowDirBlockSize.SetCoords(flowDirProperties.Width, flowDirProperties.Height);
                    IPixelBlock inPB = flowDirection.CreatePixelBlock(flowDirBlockSize);
                    IPnt pixelOrigin = new PntClass();
                    pixelOrigin.SetCoords(0, 0);
                    flowDirectionPixels.Read(pixelOrigin, inPB);
                    flowDirData = (System.Array)(inPB as IPixelBlock3).get_PixelDataByRef(0);
                }

                // If the pour point lies in a flow area, snap it to the nearest flow line and
                // enqueue the snapped point as well
                IRaster2 flowAreaRaster = m_flowArea.CreateDefaultRaster() as IRaster2;
                flowAreaRaster.MapToPixel(x, y, out col, out row);
                object flowAreaValue = flowAreaRaster.GetPixelValue(0, col, row);
                if ((flowAreaValue != null) && (!flowAreaValue.Equals((flowAreaRaster as IRasterProps).NoDataValue))) {
                    // Get flow line feature
                    int flowLineID = m_flowLine.Select(null, esriSelectionType.esriSelectionTypeIDSet, esriSelectionOption.esriSelectionOptionNormal, null).IDs.Next();
                    IPolyline flowLine = (IPolyline)m_flowLine.GetFeature(flowLineID).Shape;
                    IPoint pourPoint = new PointClass();
                    pourPoint.PutCoords(x, y);
                    IPoint outPoint = new PointClass();
                    double distanceAlongCurve = 0;
                    double distanceFromCurve = 0;
                    bool rightSide = false;
                    flowLine.QueryPointAndDistance(esriSegmentExtension.esriNoExtension,
                                                   pourPoint,
                                                   false,
                                                   outPoint,
                                                   ref distanceAlongCurve,
                                                   ref distanceFromCurve,
                                                   ref rightSide);
                    flowDirectionRaster.MapToPixel(outPoint.X, outPoint.Y, out col, out row);
                    System.Drawing.Point snappedPoint = new System.Drawing.Point(col, row);
                    if (!snappedPoint.Equals(pourPoint)) {
                        queue.Enqueue(snappedPoint);
                        visited.Add(snappedPoint);
                    }
                }
                
                IPoint worldOrigin = new PointClass();
                worldOrigin.PutCoords(flowDirProperties.Extent.XMin, flowDirProperties.Extent.YMin);
                
                byte[,] outData = new byte[flowDirProperties.Width, flowDirProperties.Height];
                outData.Initialize();
                
                // Enqueue the starting point
                flowDirectionRaster.MapToPixel(x, y, out col, out row);
                System.Drawing.Point startPoint = new System.Drawing.Point(col, row);
                queue.Enqueue(startPoint);
                visited.Add(startPoint);

                // Compute upstream area
                while (queue.Count > 0) {
                    System.Drawing.Point pt = queue.Dequeue();

                    outData.SetValue(Convert.ToByte(1), pt.X, pt.Y);
                    // right
                    testPoint(new System.Drawing.Point(pt.X + 1, pt.Y), 16, flowDirData, outData, visited, queue);
                    // lower right
                    testPoint(new System.Drawing.Point(pt.X + 1, pt.Y + 1), 32, flowDirData, outData, visited, queue);
                    // down
                    testPoint(new System.Drawing.Point(pt.X, pt.Y + 1), 64, flowDirData, outData, visited, queue);
                    // lower left
                    testPoint(new System.Drawing.Point(pt.X - 1, pt.Y + 1), 128, flowDirData, outData, visited, queue);
                    // left
                    testPoint(new System.Drawing.Point(pt.X - 1, pt.Y), 1, flowDirData, outData, visited, queue);
                    // upper left
                    testPoint(new System.Drawing.Point(pt.X - 1, pt.Y - 1), 2, flowDirData, outData, visited, queue);
                    // up
                    testPoint(new System.Drawing.Point(pt.X, pt.Y - 1), 4, flowDirData, outData, visited, queue);
                    // upper right
                    testPoint(new System.Drawing.Point(pt.X + 1, pt.Y - 1), 8, flowDirData, outData, visited, queue);
                }
                
                // Write the output data
                IRasterDataset2 outDS = ((IRasterWorkspaceEx)m_scratchWorkspace).CreateRasterDataset(
                        "UARaster" + System.Environment.TickCount.ToString(),
                        1,
                        rstPixelType.PT_UCHAR,
                        new RasterStorageDefClass(),
                        "",
                        new RasterDefClass(),
                        null) as IRasterDataset2;
                IRaster outRaster = outDS.CreateFullRaster();
                IRasterProps properties = (IRasterProps)outRaster;
                properties.Width = flowDirProperties.Width;
                properties.Height = flowDirProperties.Height;
                IEnvelope outEnvelope = new EnvelopeClass();
                outEnvelope.XMin = flowDirProperties.Extent.XMin;
                outEnvelope.XMax = flowDirProperties.Extent.XMax;
                outEnvelope.YMin = flowDirProperties.Extent.YMin;
                outEnvelope.YMax = flowDirProperties.Extent.YMax;
                properties.Extent = outEnvelope;
                properties.SpatialReference = flowDirProperties.SpatialReference;
                properties.NoDataValue = 0;
                
                IPixelBlock3 outPB = outRaster.CreatePixelBlock(flowDirBlockSize) as IPixelBlock3; 
                outPB.set_PixelData(0, outData);
                IRasterEdit outRasterEdit = outRaster as IRasterEdit;
                IPnt pbOrigin = new PntClass();
                pbOrigin.SetCoords(0, 0);
                outRasterEdit.Write(pbOrigin, (IPixelBlock)outPB);
                // Release our edit
                System.Runtime.InteropServices.Marshal.ReleaseComObject(outRasterEdit);
                    
                IConversionOp convert = new RasterConversionOpClass();
                IFeatureClass resultFC = (IFeatureClass)convert.RasterDataToPolygonFeatureData(
                                                                (IGeoDataset)outDS,
                                                                m_scratchWorkspace, 
                                                                "UAFeature" + System.Environment.TickCount.ToString(),
                                                                false
                                                            );
                int resultID = resultFC.Select(null, esriSelectionType.esriSelectionTypeIDSet, esriSelectionOption.esriSelectionOptionNormal, null).IDs.Next();
                IGeometryCollection result = (IGeometryCollection)resultFC.GetFeature(resultID).Shape;


                context.Response.ContentType = "text/plain";
                //context.Response.ContentType = "application/json";
                context.Response.StatusCode = 200;
                context.Response.Write("{\n");
                context.Response.Write("  \"results\":[\n");
                context.Response.Write("    {\n");
                context.Response.Write("      \"paramName\":\"flowpath\",\n");
                context.Response.Write("      \"dataType\":\"GPFeatureRecordSetLayer\",\n");
                context.Response.Write("      \"value\":{\n");
                context.Response.Write("        \"geometryType\":\"esriGeometryPolygpm\",\n");
                context.Response.Write("        \"spatialReference\":{\"wkid\":4326},\n");
                context.Response.Write("        \"features\":[\n");
                context.Response.Write("          {\n");
                context.Response.Write("            \"geometry\":{\n");
                context.Response.Write("              \"rings\":[\n");
                for (int i = 0; i < result.GeometryCount; i += 1) {
                    context.Response.Write("                [\n");
                    IPointCollection ring = (IPointCollection)result.get_Geometry(i);
                    for (int j = 0; j < ring.PointCount; j += 1) {
                        IPoint p = ring.get_Point(j);
                        context.Response.Write("                  [");
                        context.Response.Write(p.X.ToString());
                        context.Response.Write(", ");
                        context.Response.Write(p.Y.ToString());
                        context.Response.Write("]");
                        if ((j + 1) < ring.PointCount) {
                            context.Response.Write(",");
                        }
                        context.Response.Write("\n");
                    }
                    context.Response.Write("                ]\n");
                }
                context.Response.Write("              ]\n");
                context.Response.Write("            },\n");
                context.Response.Write("            \"attributes\":{\n");
                context.Response.Write("              \"Shape_Length\":");
                context.Response.Write(((ICurve)result).Length.ToString());
                context.Response.Write("\n");
                context.Response.Write("              \"Shape_Area\":");
                context.Response.Write(((IArea)result).Area.ToString());
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
    }
}