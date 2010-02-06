using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.GeoAnalyst;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SpatialAnalyst;

namespace NatGeo.FieldScope.WatershedTools
{
    public class QueryPolygon : GPPage
    {
        public QueryPolygon ()
            : base(esriLicenseExtensionCode.esriLicenseExtensionCodeSpatialAnalyst) { }

        override protected string Compute_Result () {
            string[] points = Context.Request.Params["polygon"].Split(',');
            string layer = Context.Request.Params["layer"];

            IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
            string workspacePath = ConfigurationManager.AppSettings["QueryPolygon.Workspace"];
            IRasterWorkspaceEx workspace = (IRasterWorkspaceEx)workspaceFactory.OpenFromFile(workspacePath, 0);
            
            IWorkspaceFactory2 scratchFactory = new InMemoryWorkspaceFactoryClass();
            IName sWName = scratchFactory.Create("", "QueryPolygon", null, 0) as IName;
            IFeatureWorkspace scratchWorkspace = sWName.Open() as IFeatureWorkspace;
            RingClass ring = new RingClass();
            object missing = Type.Missing;
            foreach (string pstring in points) {
                string[] coords = pstring.Split(' ');
                IPoint point = new PointClass();
                point.PutCoords(Double.Parse(coords[0]), Double.Parse(coords[1]));
                ring.AddPoint(point, ref missing, ref missing);
            }
            PolygonClass polygon = new PolygonClass();
            polygon.AddGeometry(ring, ref missing, ref missing);
            IFeatureClass polyFC = CreateFeatureClass(scratchWorkspace as IFeatureWorkspace, "QPZone");
            IFeature polyFeature = polyFC.CreateFeature();
            //polyFeature.set_Value(1, 0);
            polyFeature.Shape = polygon;
            polyFeature.Store();
            IFeatureClassDescriptor zone = new FeatureClassDescriptorClass();
            zone.Create(polyFC, null, "OBJECTID");
            
            IZonalOp zonalOp = new RasterZonalOpClass();
            Dictionary<string, Double> result = new Dictionary<string, double>();
            IRasterDataset layerDS = workspace.OpenRasterDataset(layer);
            IRaster layerRaster = layerDS.CreateDefaultRaster();
            ITable layerTable = (layerRaster as IRaster2).AttributeTable;
            if (layerTable.Fields.FieldCount > 3) {
                IRasterDescriptor desc = new RasterDescriptorClass();
                desc.Create(layerRaster, null, layerTable.Fields.get_Field(3).Name);
                ITable table = zonalOp.TabulateArea(zone as IGeoDataset, desc as IGeoDataset);
                IRow row = table.Search(null, true).NextRow();
                Dictionary<string, Double> temp = new Dictionary<string, double>();
                double totalArea = 0.0;
                for (int i = 2; i < table.Fields.FieldCount; i += 1) {
                    double area = Convert.ToDouble(row.get_Value(i));
                    totalArea += area;
                    temp.Add(table.Fields.get_Field(i).Name, area);
                }
                foreach (KeyValuePair<string, double> entry in temp) {
                    result.Add(entry.Key, entry.Value / totalArea);
                }
                result.Add("AREA", Math.Abs(polygon.Area));
            } else {
                ITable table = zonalOp.ZonalStatisticsAsTable(zone as IGeoDataset, layerRaster as IGeoDataset, true);
                IRow row = table.Search(null, true).NextRow();
                result.Add("COUNT", Convert.ToDouble(row.get_Value(2)));
                result.Add("AREA", Convert.ToDouble(row.get_Value(3)));
                result.Add("MIN", Convert.ToDouble(row.get_Value(4)));
                result.Add("MAX", Convert.ToDouble(row.get_Value(5)));
                result.Add("MEAN", Convert.ToDouble(row.get_Value(7)));
                result.Add("STD", Convert.ToDouble(row.get_Value(8)));
            }

            return "{\n  \"result\" : {\n" +
                    (from value in result
                     select String.Format("    \"{0}\" : {1:0.########}",
                        value.Key,
                        value.Value)
                    ).Aggregate((a, b) => a + ",\n" + b) +
                "\n  }\n}";
        }
        
		private IFeatureClass CreateFeatureClass (IFeatureWorkspace workspace, string name) {
			IFieldsEdit fields = new FieldsClass();

			IFieldEdit field = new FieldClass();
            field.Type_2 = esriFieldType.esriFieldTypeOID;
			field.Name_2 = "OBJECTID";
			field.AliasName_2 = "OBJECTID";
			fields.AddField(field);

            ISpatialReferenceFactory srFactory = new SpatialReferenceEnvironmentClass();
            ISpatialReference sr = srFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984);
            sr.SetDomain(-180, 180, -90, 90);

			IGeometryDefEdit geom = new GeometryDefClass();
			geom.GeometryType_2 = esriGeometryType.esriGeometryPolygon;
			geom.SpatialReference_2 = sr;
			geom.HasM_2 = false;
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
