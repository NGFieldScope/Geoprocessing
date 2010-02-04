using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Services;
using System.Web.Configuration;
using System.Web.Caching;
using System.Web.UI;
using System.Configuration;
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
    public abstract class GPPage : Page
    {
        protected class RasterProperties
        {
            public int Width;
            public int Height;
            public double CellWidth;
            public double CellHeight;
            public object NoDataValue;
            public ISpatialReference SpatialReference;
            public IEnvelope Extent;

            public RasterProperties (IRasterBand band) {
                IRasterProps props = band as IRasterProps;
                Width = props.Width;
                Height = props.Height;
                IPnt cellSize = props.MeanCellSize();
                CellWidth = cellSize.X;
                CellHeight = cellSize.Y;
                NoDataValue = props.NoDataValue;
                SpatialReference = (props.SpatialReference as IClone) as ISpatialReference;
                Extent = new EnvelopeClass();
                Extent.XMin = props.Extent.XMin;
                Extent.XMax = props.Extent.XMax;
                Extent.YMin = props.Extent.YMin;
                Extent.YMax = props.Extent.YMax;
            }
        }

        protected static IPoint CopyPoint (IPoint pt) {
            IPoint newPt = new PointClass();
            newPt.PutCoords(pt.X, pt.Y);
            return newPt;
        }
        
        private readonly IAoInitialize m_aoInit;

        protected GPPage () {
            m_aoInit = new AoInitializeClass();
            m_aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeArcServer);
        }

        protected GPPage (params esriLicenseExtensionCode[] extensions) : this() {
            foreach (esriLicenseExtensionCode code in extensions) {
                m_aoInit.CheckOutExtension(code);
            }
        }
    }
}
