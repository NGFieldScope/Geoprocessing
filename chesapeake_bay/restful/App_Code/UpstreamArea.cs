using System;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Web;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.GeoAnalyst;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace NatGeo.FieldScope.WatershedTools
{
    public class UpstreamArea : GPPage
    {
        protected override string Compute_Result () {

            double x = Double.Parse(HttpUtility.UrlDecode(Context.Request.Params["x"]));
            double y = Double.Parse(HttpUtility.UrlDecode(Context.Request.Params["y"]));
            
            IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
            string workspacePath = ConfigurationManager.AppSettings["UpstreamArea.Workspace"];
            IWorkspace workspace = workspaceFactory.OpenFromFile(workspacePath, 0);
            IRasterWorkspaceEx rasterWorkspace = (IRasterWorkspaceEx)workspace;
            
            IWorkspace scratchWorkspace;
            IScratchWorkspaceFactory2 scratchFactory = new ScratchWorkspaceFactoryClass();
            if (scratchFactory.CurrentScratchWorkspace != null) {
                scratchWorkspace = scratchFactory.CurrentScratchWorkspace;
            } else if (scratchFactory.DefaultScratchWorkspace != null) {
                scratchWorkspace = scratchFactory.DefaultScratchWorkspace;
            } else {
                scratchWorkspace = scratchFactory.CreateNewScratchWorkspace();
            }

            string flowAccumDS = ConfigurationManager.AppSettings["UpstreamArea.FlowAccumulation"];
            IRasterDataset flowAccum = rasterWorkspace.OpenRasterDataset(flowAccumDS);
            
            IRasterDataset[] highResFlowDir = new IRasterDataset[0];
            string hiResDS = ConfigurationManager.AppSettings["UpstreamArea.HighResolutionFlowDirection"];
            if (hiResDS != null) {
                string[] hiResDSList = hiResDS.Split(';');
                if ((hiResDSList.Length > 0) && (hiResDSList[0].Length > 0)) {
                    highResFlowDir = new IRasterDataset[hiResDSList.Length];
                    for (int i = 0; i < hiResDSList.Length; i += 1) {
                        highResFlowDir[i] = rasterWorkspace.OpenRasterDataset(hiResDSList[i]);
                    }
                }
            }
            
            Int32 highResAccumThreshold = 20;
            string threshold = ConfigurationManager.AppSettings["UpstreamArea.HighResolutionAccumThreshold"];
            if (threshold != null) {
                highResAccumThreshold = Int32.Parse(threshold);
            }

            string lowResDS = ConfigurationManager.AppSettings["UpstreamArea.LowResolutionFlowDirection"];
            IRasterDataset lowResFlowDir = rasterWorkspace.OpenRasterDataset(lowResDS);

            string flowAreaDS = ConfigurationManager.AppSettings["UpstreamArea.FlowArea"];
            IRasterDataset flowArea = rasterWorkspace.OpenRasterDataset(flowAreaDS);

            string flowLineDS = ConfigurationManager.AppSettings["UpstreamArea.FlowLine"];
            IFeatureClass flowLine = ((IFeatureWorkspace)workspace).OpenFeatureClass(flowLineDS);
            
            Queue<System.Drawing.Point> queue = new Queue<System.Drawing.Point>();
            HashSet<System.Drawing.Point> visited = new HashSet<System.Drawing.Point>();

            IRaster2 flowAccumRaster = flowAccum.CreateDefaultRaster() as IRaster2;
            int col, row;
            flowAccumRaster.MapToPixel(x, y, out col, out row);
            Int32 accum = Convert.ToInt32(flowAccumRaster.GetPixelValue(0, col, row));
            Marshal.ReleaseComObject(flowAccum);

            IRaster2 flowDirectionRaster = null;
            RasterProperties flowDirProperties = null;
            System.Array flowDirData = null;

            if (accum <= highResAccumThreshold) {
                for (int i = 0; i < highResFlowDir.Length; i += 1) {
                    IRaster flowDirection = highResFlowDir[i].CreateDefaultRaster();
                    flowDirectionRaster = flowDirection as IRaster2;
                    IRasterBand flowDirectionBand = (flowDirectionRaster as IRasterBandCollection).Item(0);
                    flowDirProperties = new RasterProperties(flowDirectionBand);
                    flowDirectionRaster.MapToPixel(x, y, out col, out row);
                    if ((col >= 0) && (col < flowDirProperties.Width) && (row >= 0) && (row < flowDirProperties.Height)) {
                        object value = flowDirectionRaster.GetPixelValue(0, col, row);
                        if ((value != null) && (!value.Equals(flowDirProperties.NoDataValue))) {
                            // use high-res raster
                            IRawPixels flowDirectionPixels = flowDirectionBand as IRawPixels;
                            IPnt flowDirBlockSize = new Pnt();
                            flowDirBlockSize.SetCoords(flowDirProperties.Width, flowDirProperties.Height);
                            IPixelBlock inPB = flowDirection.CreatePixelBlock(flowDirBlockSize);
                            IPnt pixelOrigin = new Pnt();
                            pixelOrigin.SetCoords(0, 0);
                            flowDirectionPixels.Read(pixelOrigin, inPB);
                            flowDirData = (System.Array)(inPB as IPixelBlock3).get_PixelDataByRef(0);
                            break;
                        }
                    }
                    Marshal.ReleaseComObject(flowDirectionRaster);
                }
            }

            if (flowDirData == null) {
                // Get low resolution flow direction raster
                IRaster flowDirection = lowResFlowDir.CreateDefaultRaster();
                flowDirectionRaster = flowDirection as IRaster2;
                IRasterBand flowDirectionBand = (flowDirectionRaster as IRasterBandCollection).Item(0);
                IRawPixels flowDirectionPixels = flowDirectionBand as IRawPixels;
                flowDirProperties = new RasterProperties(flowDirectionBand);
                IPnt flowDirBlockSize = new Pnt();
                flowDirBlockSize.SetCoords(flowDirProperties.Width, flowDirProperties.Height);
                IPixelBlock inPB = flowDirection.CreatePixelBlock(flowDirBlockSize);
                IPnt pixelOrigin = new Pnt();
                pixelOrigin.SetCoords(0, 0);
                flowDirectionPixels.Read(pixelOrigin, inPB);
                flowDirData = (System.Array)(inPB as IPixelBlock3).get_PixelDataByRef(0);
            }

            // If the pour point lies in a flow area, snap it to the nearest flow line and
            // enqueue the snapped point as well
            IRaster2 flowAreaRaster = flowArea.CreateDefaultRaster() as IRaster2;
            flowAreaRaster.MapToPixel(x, y, out col, out row);
            RasterProperties flowAreaProps = new RasterProperties((flowDirectionRaster as IRasterBandCollection).Item(0));
            object flowAreaValue = flowAreaRaster.GetPixelValue(0, col, row);
            if ((flowAreaValue != null) && (!flowAreaValue.Equals(flowAreaProps.NoDataValue))) {
                // Get flow line feature
                int flowLineID = flowLine.Select(null, esriSelectionType.esriSelectionTypeIDSet, esriSelectionOption.esriSelectionOptionNormal, null).IDs.Next();
                IPolyline polyline = (IPolyline)flowLine.GetFeature(flowLineID).Shape;
                IPoint pourPoint = new PointClass();
                pourPoint.PutCoords(x, y);
                IPoint outPoint = new PointClass();
                double distanceAlongCurve = 0;
                double distanceFromCurve = 0;
                bool rightSide = false;
                polyline.QueryPointAndDistance(esriSegmentExtension.esriNoExtension,
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
                Marshal.ReleaseComObject(flowLine);
            }
            Marshal.ReleaseComObject(flowAreaRaster);


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
            IPoint worldOrigin = new PointClass();
            worldOrigin.PutCoords(flowDirProperties.Extent.XMin, flowDirProperties.Extent.YMin);
            IRasterDataset2 outDS = ((IRasterWorkspace2)scratchWorkspace).CreateRasterDataset(
                    "UARaster" + System.Environment.TickCount.ToString(),
                    "MEM",
                    worldOrigin,
                    flowDirProperties.Width,
                    flowDirProperties.Height,
                    flowDirProperties.CellWidth,
                    flowDirProperties.CellHeight,
                    1,
                    rstPixelType.PT_UCHAR,
                    flowDirProperties.SpatialReference,
                    false
                ) as IRasterDataset2;
            IRaster outRaster = outDS.CreateDefaultRaster();
            ((outDS as IRasterBandCollection).Item(0) as IRasterProps).NoDataValue = 0;
            IPnt outBlockSize = new Pnt();
            outBlockSize.SetCoords(flowDirProperties.Width, flowDirProperties.Height);
            IPixelBlock3 outPB = outRaster.CreatePixelBlock(outBlockSize) as IPixelBlock3;
            outPB.set_PixelData(0, outData);
            IRasterEdit outRasterEdit = outRaster as IRasterEdit;
            IPnt pbOrigin = new Pnt();
            pbOrigin.SetCoords(0, 0);
            outRasterEdit.Write(pbOrigin, (IPixelBlock)outPB);
            // Release our edit
            Marshal.ReleaseComObject(outRasterEdit);

            IConversionOp convert = new RasterConversionOpClass();
            IFeatureClass resultFC = (IFeatureClass)convert.RasterDataToPolygonFeatureData(
                    (IGeoDataset)outDS,
                    scratchWorkspace,
                    "UAFeature" + System.Environment.TickCount.ToString(),
                    false
                );
            int resultID = resultFC.Select(null, esriSelectionType.esriSelectionTypeIDSet, esriSelectionOption.esriSelectionOptionNormal, null).IDs.Next();
            IGeometryCollection gc = (IGeometryCollection)resultFC.GetFeature(resultID).Shape;

            string result = 
                "{\n" +
                "  \"result\" : {\n" +
                "    \"geometryType\" : \"esriGeometryPolygon\",\n" +
                "    \"spatialReference\" : { \"wkid\" : 4326 },\n" +
                "    \"features\" : [\n" +
                "      {\n" +
                "        \"geometry\" : {\n" +
                "          \"rings\" : [\n";
            for (int i = 0; i < gc.GeometryCount; i += 1) {
                result += "            [\n";
                IPointCollection ring = (IPointCollection)gc.get_Geometry(i);
                for (int j = 0; j < ring.PointCount; j += 1) {
                    IPoint p = ring.get_Point(j);
                    result += String.Format("              [ {0:0.################}, {1:0.################} ]{2}\n",
                                            p.X,
                                            p.Y,
                                            ((j == (ring.PointCount - 1)) ? "" : ","));
                }
                result += "            ]\n";
            }
            result +=
                "          ]\n" +
                "        },\n" +
                "        \"attributes\" : {\n" +
                String.Format("          \"Shape_Length\" : {0:0.################},\n" +
                              "          \"Shape_Area\" : {1:0.################}\n",
                              ((ICurve)gc).Length,
                              ((IArea)gc).Area) +
                "        }\n" +
                "      }\n" +
                "    ]\n" +
                "  }\n" +
                "}\n";

            Marshal.ReleaseComObject(resultFC);

            Marshal.ReleaseComObject(flowAccum);
            foreach (object raster in highResFlowDir) {
                Marshal.ReleaseComObject(raster);
            }
            Marshal.ReleaseComObject(lowResFlowDir);
            Marshal.ReleaseComObject(flowArea);
            Marshal.ReleaseComObject(flowLine);
            Marshal.ReleaseComObject(scratchWorkspace);

            return result;
        }

        private void testPoint (System.Drawing.Point pt,
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
    }
}