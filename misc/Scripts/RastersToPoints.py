
# Import system modules
import sys, string, os, math, arcgisscripting, arcpy
from arcpy import *
from arcpy.sa import *

env.workspace = "C:\\Documents and Settings\\erussell\\My Documents\\ArcGIS\\Default.gdb"

arcpy.CheckOutExtension("spatial")

# Create the Geoprocessor object
gp = arcgisscripting.create(10)
    
input_rasters = "C:\\Documents and Settings\\erussell\\My Documents\\ArcGIS\\Saguaro\\housing_density\\data.gdb\\hdd1950;C:\\Documents and Settings\\erussell\\My Documents\\ArcGIS\\Saguaro\\housing_density\\data.gdb\\hdd1960"
output_points = "C:\\Documents and Settings\\erussell\\My Documents\\ArcGIS\\Saguaro\\housing_density\\data.gdb\\test_output_1"

def doIt ():
    input = input_rasters.split(";")
    input_list = map(lambda raster: [raster, os.path.basename(raster)], input)
    print input_list
    where_clause = " AND ".join(map(lambda raster: "(\"%s\" IS NULL OR \"%s\" = 0)" % (raster[1], raster[1]), input_list))
    print where_clause
    isnull_raster = IsNull(input[0])
    RasterToPoint_conversion(isnull_raster, output_points, "COUNT")
    ExtractMultiValuesToPoints(output_points, input_list, "FALSE")
    output_layer = "%s_layer" %  os.path.basename(output_points)
    MakeFeatureLayer_management(output_points, output_layer)
    SelectLayerByAttribute_management(output_layer, "NEW_SELECTION", where_clause)
    DeleteFeatures_management(output_layer)
    print "finished"
doIt()