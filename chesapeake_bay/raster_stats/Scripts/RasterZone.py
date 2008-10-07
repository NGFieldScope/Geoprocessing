# ---------------------------------------------------------------------------
# 
# ---------------------------------------------------------------------------

# Import system modules
import sys, string, os, arcgisscripting

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
    inputRaster = toolDataGDB + os.path.sep + rasterName
    outTable = sys.argv[3]

    # create the out table if it doesn't exist
    try:
        gp.describe(outTable)
    except Exception, err:
        gp.createtable_management(os.path.split(outTable)[0], os.path.split(outTable)[1], toolDataGDB + "result_schema")
    outRows = gp.insertcursor(outTable)
    
    gp.CheckOutExtension("spatial")
    
    if rasterName in [ "landcover" ]:
        # Indexed raster
        tabulation = gp.createscratchname("tabulation", "", "Table", scratchWorkspace)
        gp.tabulatearea_sa(inputFC, "ID", inputRaster, "Value", tabulation)
        categoryNames = []
        rasterRows = gp.searchcursor(inputRaster)
        rasterRow = rasterRows.next()
        while rasterRow:
            categoryNames.append(rasterRow.getvalue("CLASSNAME"))
            rasterRow = rasterRows.next();
        del rasterRows
        
        tabFields = gp.listfields(tabulation)
        tabRows = gp.searchcursor(tabulation)
        tabRow = tabRows.next()
        areas = map(lambda field: tabRow.getvalue(field.Name), tabFields[2:])
        areaSum = sum(areas)
        def saveArea (area, field):
            outRow = outRows.newrow()
            category = categoryNames[int(field.Name.rpartition("_")[2]) - 1]
            outRow.setvalue("Name", category)
            value = area / areaSum
            outRow.setvalue("Value", value)
            outRows.insertrow(outRow)
            del outRow
            return value
        map(saveArea, areas, tabFields[2:])
        del tabRows
        gp.delete_management(tabulation, "Table")
    else:
        stats = gp.createscratchname("zonalStats", "", "Table", scratchWorkspace)
        gp.zonalstatisticsastable_sa(inputFC, "ID", inputRaster, stats)
        statFields = gp.listfields(stats)
        statRows = gp.searchcursor(stats)
        statRow = statRows.next()
        for statField in statFields[2:]:
            outRow = outRows.newrow()
            outRow.setvalue("Name", statField.Name)
            outRow.setvalue("Value", statRow.getvalue(statField.Name))
            outRows.insertrow(outRow)
            del outRow
        del statRow
        del statRows
        gp.delete_management(stats, "Table")
    del outRows
    gp.CheckInExtension("spatial")
except Exception, err:
    gp.AddError(str(err))
    gp.AddError(gp.getmessages(2))
