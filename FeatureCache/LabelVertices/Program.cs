using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataManagementTools;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.GeoAnalyst;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace LabelVertices
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

            IFeatureClass inFC = workspace.OpenFeatureClass("test");

            CopyAndLabel(inFC, workspace, "labeled_0");
        }

        static void CopyAndLabel (IFeatureClass inFC, IFeatureWorkspace destination, String name) {
            object missing = Type.Missing;
            IFieldsEdit outFields = new FieldsClass();
            ISpatialReference outSR = null;
            for (int i = 0; i < inFC.Fields.FieldCount; i += 1) {
                IField field = inFC.Fields.get_Field(i);
                if (field.Type == esriFieldType.esriFieldTypeGeometry) {
                    outSR = field.GeometryDef.SpatialReference;
                } else {
                    outFields.AddField(field);
                }
            }
            outSR.SetMDomain(-137434824702, 137434824702);

            IGeometryDefEdit geom = new GeometryDefClass();
            geom.GeometryType_2 = esriGeometryType.esriGeometryPolygon;
            geom.SpatialReference_2 = outSR;
            geom.HasM_2 = true;
            geom.HasZ_2 = false;

            IFieldEdit geomField = new FieldClass();
            geomField.Name_2 = "SHAPE";
            geomField.AliasName_2 = "SHAPE";
            geomField.Type_2 = esriFieldType.esriFieldTypeGeometry;
            geomField.GeometryDef_2 = geom;
            outFields.AddField(geomField);

            IFeatureClass outFC = destination.CreateFeatureClass(name, outFields, null, null, esriFeatureType.esriFTSimple, "SHAPE", "");
            // Start numbering from 1, because index 0 is used by clipping cell
            int vIndex = 1;
            IFeatureCursor featureCursor = inFC.Search(null, true);
            IFeature inFeature;
            while ((inFeature = featureCursor.NextFeature()) != null) {
                IFeature outFeature = outFC.CreateFeature();
                for (int i = 0; i < outFields.FieldCount; i += 1) {
                    IField field = outFields.Field[i];
                    if (field.Editable && (field.Type != esriFieldType.esriFieldTypeGeometry)) {
                        outFeature.set_Value(i, inFeature.get_Value(i));
                    }
                }
                IPolygon4 inShape = inFeature.Shape as IPolygon4;
                PolygonClass outShape = new PolygonClass();

                IGeometryBag extRingBag = inShape.ExteriorRingBag;
                IGeometryCollection extRings = extRingBag as IGeometryCollection;
                for (int i = 0; i < extRings.GeometryCount; i += 1) {
                    IGeometry inExtRingGeom = extRings.get_Geometry(i);
                    IPointCollection inExtRing = inExtRingGeom as IPointCollection;
                    RingClass outExtRing = new RingClass();
                    for (int j = 0; j < inExtRing.PointCount; j += 1) {
                        IPoint point = inExtRing.get_Point(j);
                        point.M = vIndex++;
                        outExtRing.AddPoint(point);
                    }
                    outShape.AddGeometry(outExtRing);
                    IGeometryBag intRingBag = inShape.get_InteriorRingBag(inExtRingGeom as IRing);
                    IGeometryCollection intRings = intRingBag as IGeometryCollection;
                    for (int j = 0; j < intRings.GeometryCount; j += 1) {
                        IGeometry intRingGeom = intRings.get_Geometry(j);
                        IPointCollection inIntRing = intRingGeom as IPointCollection;
                        RingClass outIntRing = new RingClass();
                        for (int k = 0; k < inIntRing.PointCount; k += 1) {
                            IPoint point = inExtRing.get_Point(k);
                            point.M = vIndex++;
                            outIntRing.AddPoint(point);
                        }
                        outShape.AddGeometry(outIntRing);
                    }
                }
                outFeature.Shape = outShape;
                outFeature.Store();
            }
        }
    }
}
