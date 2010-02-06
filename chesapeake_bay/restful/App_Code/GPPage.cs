using System;
using System.Web.UI;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

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

        protected abstract string Compute_Result ();

        protected void Page_Load (object sender, EventArgs evt) {
            if (Context.IsDebuggingEnabled) {
                Response.ContentType = "text/plain";
            } else {
                Response.ContentType = "application/json";
            }
            try {
                string result = Compute_Result();
                Response.StatusCode = 200;
                Response.Write(result);
            } catch (Exception e) {
                // This really ought to be status code 500, but Flex is too stupid
                // to retrive the error details if we return a status other than OK
                Response.StatusCode = 200;
                Response.Write(
                    "{\n  \"error\" : {\n    \"type\" : \"" +
                    e.GetType().ToString().Replace("\"", "\\\"") +
                    "\",\n    \"message\" : \"" +
                    e.Message.Replace("\"", "\\\"") +
                    "\",\n    \"stackTrace\" : \"" +
                    e.StackTrace.Replace("\"", "\\\"") +
                    "\"\n  }\n}"
                );
            }
        }
    }
}
