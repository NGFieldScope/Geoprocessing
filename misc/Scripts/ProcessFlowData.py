import sys
import string
import os
import arcpy
import shutil

# Process elevation data into necessary flow direction and accumulation
# rasters for use by the FlowPath and Watershed tools. 

# Input parameters - elevation and 8-digit watersheds. Both must be in
# the same spatial reference system. The HUC8 dataset must have a 
# field named "HUC8" that contains the 8-digit hydrologic unit code for
# each record.
elevation_src = "D:/test/n35w080_dem/n35w080_dem"
huc8_src = "D:/test/data.gdb/HUC8_prj"

# Output desintations
flowpath_folder = "D:/test/flowpath"
watershed_folder = "D:/test/watershed"

arcpy.CheckOutExtension("Spatial")
folder = os.path.dirname(os.path.abspath(__file__))
scratch_folder = os.path.abspath(os.path.join(os.path.join(folder, ".."), "Scratch"))
data_folder = os.path.abspath(os.path.join(os.path.join(folder, ".."), "ToolData"))
arcpy.env.scratchWorkspace = os.path.join(scratch_folder, 'scratch.gdb')
arcpy.env.workspace = arcpy.env.scratchWorkspace
arcpy.env.overwriteOutput = True
arcpy.env.outputCoordinateSystem = arcpy.Describe(elevation_src).spatialReference
arcpy.env.extent = "MINOF"
map_template = os.path.join(data_folder, "Blank.mxd")

if not os.path.exists(flowpath_folder):
    os.mkdir(flowpath_folder)
flowpath_workspace = os.path.join(flowpath_folder, "data.gdb")
if not os.path.exists(flowpath_workspace):
    arcpy.management.CreateFileGDB(os.path.dirname(flowpath_workspace), os.path.basename(flowpath_workspace))
flowpath_map = os.path.join(flowpath_folder, "flowpath.mxd")
if os.path.exists(flowpath_map):
    os.remove(flowpath_map)
shutil.copyfile(map_template, flowpath_map)
flowpath_mxd = arcpy.mapping.MapDocument(flowpath_map)
flowpath_df = arcpy.mapping.ListDataFrames(flowpath_mxd, "*")[0]

if not os.path.exists(watershed_folder):
    os.mkdir(watershed_folder)
watershed_workspace = os.path.join(watershed_folder, "data.gdb")
if not os.path.exists(watershed_workspace):
    arcpy.management.CreateFileGDB(os.path.dirname(watershed_workspace), os.path.basename(watershed_workspace))
watershed_map = os.path.join(watershed_folder, "watershed.mxd")
if os.path.exists(watershed_map):
    os.remove(watershed_map)
shutil.copyfile(map_template, watershed_map)
watershed_mxd = arcpy.mapping.MapDocument(watershed_map)
watershed_df = arcpy.mapping.ListDataFrames(watershed_mxd, "*")[0]

elevation_30sec = os.path.join(arcpy.env.scratchWorkspace, "Elevation_30sec")
arcpy.management.Resample(elevation_src, elevation_30sec, 0.008333, "BILINEAR")
clip_30sec = arcpy.sa.ExtractByMask(elevation_30sec, huc8_src)
fill_30sec = arcpy.sa.Fill(clip_30sec)
flowdir_30sec = arcpy.sa.FlowDirection(fill_30sec)
flowdir_30sec_flowpath = os.path.join(flowpath_workspace, "FlowDirection_30sec")
flowdir_30sec.save(flowdir_30sec_flowpath)
arcpy.mapping.AddLayer(flowpath_df, arcpy.mapping.Layer(flowdir_30sec_flowpath), "BOTTOM")

flowaccum_30sec = arcpy.sa.FlowAccumulation(flowdir_30sec, '', 'INTEGER')
flowcomposite_30sec_watershed = os.path.join(watershed_workspace, "FlowComposite_30sec")
arcpy.management.CompositeBands("%s;%s" % (flowaccum_30sec, flowdir_30sec), flowcomposite_30sec_watershed)
arcpy.mapping.AddLayer(watershed_df, arcpy.mapping.Layer(flowcomposite_30sec_watershed), "BOTTOM")

flowpath_index = os.path.join(flowpath_workspace, "FlowDirection_5sec_index")
flowpath_index_fc = arcpy.management.CreateFeatureclass(os.path.dirname(flowpath_index),
                                                        os.path.basename(flowpath_index), 
                                                        "POLYGON", '', '', '', huc8_src)
arcpy.management.AddField(flowpath_index_fc, 'VALUE', 'TEXT', '', '', 50)
index_flowpath_cursor = arcpy.InsertCursor(flowpath_index_fc)
arcpy.mapping.AddLayer(flowpath_df, arcpy.mapping.Layer(flowpath_index), "BOTTOM")

watershed_index = os.path.join(watershed_workspace, "FlowComposite_5sec_index")
watershed_index_fc = arcpy.management.CreateFeatureclass(os.path.dirname(watershed_index), 
                                                          os.path.basename(watershed_index), 
                                                          "POLYGON", '', '', '', huc8_src)
arcpy.management.AddField(watershed_index_fc, 'VALUE', 'TEXT', '', '', 50)
index_watershed_cursor = arcpy.InsertCursor(watershed_index_fc)
arcpy.mapping.AddLayer(watershed_df, arcpy.mapping.Layer(watershed_index), "BOTTOM")

flowline_raster = arcpy.sa.Con(flowaccum_30sec, 1, 0, '"VALUE" > 300')
flowline_30sec_watershed = os.path.join(watershed_workspace, "FlowLine_30sec")
arcpy.conversion.RasterToPolyline(flowline_raster, flowline_30sec_watershed, "ZERO", 0, "NO_SIMPLIFY")

flowpath_catalog = os.path.join(flowpath_workspace, "FlowDirection_5sec")
flowpath_catalog_rc = arcpy.management.CreateRasterCatalog(os.path.dirname(flowpath_catalog),
                                                           os.path.basename(flowpath_catalog),
                                                           flowline_raster.spatialReference,
                                                           flowline_raster.spatialReference)
arcpy.mapping.AddLayer(flowpath_df, arcpy.mapping.Layer(flowpath_catalog), "BOTTOM")

watershed_catalog = os.path.join(watershed_workspace, "FlowComposite_5sec")
watershed_catalog_rc = arcpy.management.CreateRasterCatalog(os.path.dirname(watershed_catalog),
                                                           os.path.basename(watershed_catalog),
                                                           flowline_raster.spatialReference,
                                                           flowline_raster.spatialReference)
arcpy.mapping.AddLayer(watershed_df, arcpy.mapping.Layer(watershed_catalog), "BOTTOM")

flowarea = os.path.join(watershed_workspace, "FlowArea_30sec")
flowarea_raster = arcpy.sa.CreateConstantRaster(255, "INTEGER", 0.008333, flowline_raster.extent)
flowarea_raster = arcpy.sa.SetNull(flowarea_raster, flowarea_raster)
flowarea_raster.save(flowarea)
#arcpy.management.DefineProjection(flowarea, flowline_raster.spatialReference)
arcpy.mapping.AddLayer(watershed_df, arcpy.mapping.Layer(flowarea), "BOTTOM")
arcpy.mapping.AddLayer(watershed_df, arcpy.mapping.Layer(flowline_30sec_watershed), "BOTTOM")

huc8s = arcpy.SearchCursor(huc8_src)
for huc8 in huc8s:
    code = str(huc8.HUC8)
    mask_layer = os.path.join(arcpy.env.scratchWorkspace, "msk_%s" % code)
    arcpy.management.MakeFeatureLayer(huc8_src, code, "\"HUC8\" = '%s'" % code)
    arcpy.management.CopyFeatures(code, mask_layer)
    elevation_masked = arcpy.sa.ExtractByMask(elevation_src, mask_layer)
    elevation_5sec = os.path.join(arcpy.env.scratchWorkspace, "e5_%s" % code)
    arcpy.management.Resample(elevation_masked, elevation_5sec, 0.001389, "BILINEAR")
    fill_5sec = arcpy.sa.Fill(elevation_5sec)
    flowdir_5sec = arcpy.sa.FlowDirection(fill_5sec)
    flowdir_name = "FlowDirection_%s" % code
    flowdir_5sec_flowpath = os.path.join(flowpath_workspace, flowdir_name)
    flowdir_5sec.save(flowdir_5sec_flowpath)
    flowdir_idx = index_flowpath_cursor.newRow()
    flowdir_idx.shape = huc8.shape
    flowdir_idx.VALUE = flowdir_name
    index_flowpath_cursor.insertRow(flowdir_idx)
    del flowdir_idx
    arcpy.conversion.RasterToGeodatabase(flowdir_5sec_flowpath, flowpath_catalog_rc)
    
    flowaccum_5sec = arcpy.sa.FlowAccumulation(flowdir_5sec, '', 'INTEGER')
    flowcomp_name = "FlowComposite_%s" % code
    flowcomposite_5sec_watershed = os.path.join(watershed_workspace, flowcomp_name)
    arcpy.management.CompositeBands("%s;%s" % (flowaccum_5sec, flowdir_5sec), flowcomposite_5sec_watershed)
    watershed_idx = index_watershed_cursor.newRow()
    watershed_idx.shape = huc8.shape
    watershed_idx.VALUE = flowcomp_name
    index_watershed_cursor.insertRow(watershed_idx)
    del watershed_idx
    arcpy.conversion.RasterToGeodatabase(flowcomposite_5sec_watershed, watershed_catalog_rc)
    
    arcpy.management.Delete(mask_layer)
    arcpy.management.Delete(elevation_5sec)
    del huc8
del index_flowpath_cursor
del index_watershed_cursor

flowpath_mxd.save()
watershed_mxd.save()


