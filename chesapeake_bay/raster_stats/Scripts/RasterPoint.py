# ---------------------------------------------------------------------------
# 
# ---------------------------------------------------------------------------

# E:/GIS/cb_gp_watershed/test_point_1.shp E:/GIS/scripts/chesapeake_bay/raster_stats/Scratch/scratch.gdb/test_pt_out

# Import system modules
import time, sys, string, os, arcgisscripting

# Create the Geoprocessor object
gp = arcgisscripting.create(9.3)
gp.overwriteoutput = 1

try:
    toolShareFolder = os.path.dirname(sys.path[0]) + os.path.sep
    toolDataFolder = toolShareFolder + "ToolData" + os.path.sep
    toolDataGDB = toolDataFolder + "ToolData.gdb" + os.path.sep
    scratchGDB = toolShareFolder + "Scratch" + os.path.sep + "scratch.gdb"
    scratchWorkspace = gp.scratchworkspace or scratchGDB
    
    inputFC = sys.argv[1]
    rasterName = sys.argv[2]
    inputRaster = toolDataFolder + os.path.sep + rasterName + ".img"
    outTable = sys.argv[3]

    # Read the first point from the input feature class    
    rows = gp.searchcursor(inputFC)
    row = rows.next()
    point = row.getvalue(gp.describe(inputFC).ShapeFieldName).getpart()
    samplePt = str(point.x) + " " + str(point.y)
    
    # create the out table if it doesn't exist
    try:
        gp.describe(outTable)
    except Exception, err:
        gp.createtable_management(os.path.split(outTable)[0], os.path.split(outTable)[1], toolDataGDB + "point_result_schema")
    outRows = gp.insertcursor(outTable)
    
    gp.CheckOutExtension("spatial")
    startTime = time.clock()
    value = gp.GetCellValue_management(inputRaster, samplePt).getoutput(0)
    if (value != "NoData"):
        if rasterName in [ "landcover" ]:
            outRow = outRows.newrow()
            outRow.setValue("Layer", rasterName)
            categoryNames = []
            rasterRows = gp.searchcursor(inputRaster)
            rasterRow = rasterRows.next()
            while rasterRow:
                categoryNames.append(rasterRow.getvalue("CLASSNAME"))
                rasterRow = rasterRows.next();
            del rasterRows
            outRow.setvalue("Value", categoryNames[int(value)])
            outRows.insertrow(outRow)
            del outRow
        else:
            outRow = outRows.newrow()
            outRow.setValue("Layer", rasterName)
            outRow.setvalue("Value", value)
            outRows.insertrow(outRow)
            del outRow
    del outRows
    gp.addmessage("Execution time=" + str(time.clock() - startTime) + " seconds")
    gp.CheckInExtension("spatial")
except Exception, err:
    gp.AddError(str(err))
    gp.AddError(gp.getmessages(2))
