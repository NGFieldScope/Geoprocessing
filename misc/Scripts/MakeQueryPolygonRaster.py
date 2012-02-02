import sys, string, os, arcpy

in_raster = "Y:/arcgisserver/data/ew_elevation/data.gdb/elevation_proj"
out_raster = "Y:/arcgisserver/data/ew_elevation/data.gdb/elevation_cm"

arcpy.CheckOutExtension("Spatial")
parent = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
scratch_folder = os.path.join(parent, "Scratch")
data_folder = os.path.join(parent, "ToolData")
arcpy.env.scratchWorkspace = os.path.join(scratch_folder, 'scratch.gdb')
arcpy.env.workspace = arcpy.env.scratchWorkspace
arcpy.env.overwriteOutput = True

min = float(arcpy.GetRasterProperties_management(in_raster, "MINIMUM").getOutput(0))
max = float(arcpy.GetRasterProperties_management(in_raster, "MAXIMUM").getOutput(0))
scale = 255.0 / (max - min)

result = arcpy.sa.Times(arcpy.sa.Minus(in_raster, min), scale)
arcpy.CopyRaster_management(result,out_raster,"DEFAULTS","#","#","#","#","8_BIT_UNSIGNED")
arcpy.AddColormap_management(out_raster,"#",os.path.join(data_folder,"gray.clr"))

print "<cellSize>%s</cellSize>" % arcpy.GetRasterProperties_management(out_raster, "CELLSIZEX")
print "<top>%s</top>" % arcpy.GetRasterProperties_management(out_raster, "TOP")
print "<left>%s</left>" % arcpy.GetRasterProperties_management(out_raster, "LEFT")
print "<scale>%s</scale>" % (1.0/scale)
print "<offset>%s</offset>" % min
