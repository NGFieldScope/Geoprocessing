import sys, string, os, arcpy

# Process elevation data into necessary flow direction and accumulation
# rasters for use by the FlowPath and Watershed tools. 

# Input parameters - elevation and 8-digit watersheds. Both must be in
# the same spatial reference system. The HUC8 dataset must have a 
# field named "HUC8" that contains the 8-digit hydrologic unit code for
# each record.
elevation_src = "C:/Users/Administrator/Documents/ArcGIS/Default.gdb/hydrosheds_elev"
huc8_src = "C:/Users/Administrator/Documents/ArcGIS/Default.gdb/huc8_wa_2"

# Output desintations
flowpath_workspace = "Y:/arcgisserver/data/ew_flowpath.gdb"
watershed_workspace = "Y:/arcgisserver/data/ew_watershed.gdb"

arcpy.CheckOutExtension("Spatial")
folder = os.path.dirname(os.path.abspath(__file__))
scratch_folder = os.path.abspath(os.path.join(os.path.join(folder, ".."), "Scratch"))
arcpy.env.scratchWorkspace = os.path.join(scratch_folder, 'scratch.gdb')
arcpy.env.workspace = arcpy.env.scratchWorkspace
arcpy.env.overwriteOutput = True

elevation_30sec = os.path.join(arcpy.env.scratchWorkspace, "elev_30sec")
arcpy.management.Resample(elevation_src, elevation_30sec, 0.008333, "BILINEAR")
fill_30sec = arcpy.sa.Fill(elevation_30sec)
flowdir_30sec = arcpy.sa.FlowDirection(fill_30sec)
flowdir_30sec_flowpath = os.path.join(flowpath_workspace, "fldir_30sec")
flowdir_30sec.save(flowdir_30sec_flowpath)
flowaccum_30sec = arcpy.sa.FlowAccumulation(flowdir_30sec, '', 'INTEGER')
flowcomposite_30sec_watershed = os.path.join(watershed_workspace, "flcmp_30sec")
arcpy.management.CompositeBands("%s;%s" % (flowaccum_30sec, flowdir_30sec), flowcomposite_30sec_watershed)
flowline_raster = arcpy.sa.Con(flowaccum_30sec, 1, 0, '"VALUE" > 300')
flowline_30sec_watershed = os.path.join(watershed_workspace, "fline_30sec")
arcpy.conversion.RasterToPolyline(flowline_raster, flowline_30sec_watershed, "ZERO", 0, "NO_SIMPLIFY")
flowarea = os.path.join(watershed_workspace, "farea_30sec")
flowarea_raster = arcpy.sa.CreateConstantRaster(255, "INTEGER", 0.008333, flowline_raster.extent)
flowarea_raster = arcpy.sa.SetNull(flowarea_raster, flowarea_raster)
flowarea_raster.save(flowarea)
arcpy.management.DefineProjection(flowarea, flowline_raster.spatialReference)

index_flowpath = arcpy.management.CreateFeatureclass(flowpath_workspace, "fd_5sec_idx", "POLYGON", '', '', '', huc8_src)
arcpy.management.AddField(index_flowpath, 'VALUE', 'TEXT', '', '', 50)
index_flowpath_cursor = arcpy.InsertCursor(index_flowpath)
index_watershed = arcpy.management.CreateFeatureclass(watershed_workspace, "fc_5sec_idx", "POLYGON", '', '', '', huc8_src)
arcpy.management.AddField(index_watershed, 'VALUE', 'TEXT', '', '', 50)
index_watershed_cursor = arcpy.InsertCursor(index_watershed)

huc8s = arcpy.SearchCursor(huc8_src)
for huc8 in huc8s:
	code = str(huc8.HUC_8)
	layer = os.path.join(scratch_folder, "%s.lyr" % code)
	arcpy.management.MakeFeatureLayer(huc8_src, layer, "\"HUC_8\" = '%s'" % code)
	elevation_masked = arcpy.sa.ExtractByMask(elevation_src, layer)
	elevation_5sec = os.path.join(arcpy.env.scratchWorkspace, "e5_%s" % code)
	arcpy.management.Resample(elevation_masked, elevation_5sec, 0.001389, "BILINEAR")
	fill_5sec = arcpy.sa.Fill(elevation_5sec)
	flowdir_5sec = arcpy.sa.FlowDirection(fill_5sec)
	flowdir_5sec_flowpath = os.path.join(flowpath_workspace, "fd_%s" % code)
	flowdir_5sec.save(flowdir_5sec_flowpath)
	flowdir_idx = index_flowpath_cursor.newRow()
	flowdir_idx.shape = huc8.shape
	flowdir_idx.VALUE = "fd_%s" % code
	index_flowpath_cursor.insertRow(flowdir_idx)
	del flowdir_idx
	flowaccum_5sec = arcpy.sa.FlowAccumulation(flowdir_5sec, '', 'INTEGER')
	flowcomposite_5sec_watershed = os.path.join(watershed_workspace, "fc_%s" % code)
	arcpy.management.CompositeBands("%s;%s" % (flowaccum_5sec, flowdir_5sec), flowcomposite_5sec_watershed)
	watershed_idx = index_watershed_cursor.newRow()
	watershed_idx.shape = huc8.shape
	watershed_idx.VALUE = "fc_%s" % code
	index_watershed_cursor.insertRow(watershed_idx)
	del watershed_idx
	arcpy.management.Delete(layer)
	del huc8
del index_flowpath_cursor
del index_watershed_cursor
