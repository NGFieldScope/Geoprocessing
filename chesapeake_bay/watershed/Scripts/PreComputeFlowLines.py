# ---------------------------------------------------------------------------
#
# ---------------------------------------------------------------------------

# E:/GIS/cb_gp_watershed/data.gdb/stream_points_flow_500_30sec POINTID FLOW E:/GIS/cb_gp_watershed/flow_dir_30sec.img E:/GIS/cb_gp_watershed/stream_seg_id_500_30sec.img E:/GIS/cb_gp_watershed/data.gdb/test_computed_segs


# Import system modules
import sys, string, os, arcgisscripting, time

# Create the Geoprocessor object
gp = arcgisscripting.create(9.3)
gp.overwriteoutput = 1

try:
    gp.toolbox = "management"

    # ToolShare folders    
    toolShareFolder = os.path.dirname(sys.path[0]) + os.path.sep
    toolDataFolder = toolShareFolder + "ToolData" + os.path.sep
    toolDataGDB = toolDataFolder + "ToolData.gdb" + os.path.sep
    
    # Script arguments...
    inputFC = sys.argv[1]
    idField = sys.argv[2]
    flowField = sys.argv[3]
    flowDir = sys.argv[4]
    segIdRaster = sys.argv[5]
    outputFC = sys.argv[6]
    
    # Read points from the input feature class
    inShapeField = gp.describe(inputFC).ShapeFieldName
    inRows = gp.searchcursor(inputFC, "", "", "", flowField + " D")
    inRow = inRows.next()
    
    # get the cell size of the flow direction raster
    flowDesc = gp.describe(flowDir)
    cellWidth = flowDesc.MeanCellWidth
    cellHeight = flowDesc.MeanCellHeight
    
    # create the output feature class if it doesn't exist already
    try:
        gp.describe(outputFC)
    except Exception, err:
        outputSR = gp.CreateObject("spatialreference")
        outputSR.CreateFromFile(toolDataFolder + "OutputSpatialReference.prj")
        gp.CreateFeatureclass_management(os.path.split(outputFC)[0], os.path.split(outputFC)[1], "POLYLINE", "#", "DISABLED", "DISABLED", outputSR)
        gp.addfield(outputFC, idField, "LONG")
    
    # create cursor for writing to the output feature class
    outShapeField = gp.describe(outputFC).ShapeFieldName
    outRows = gp.insertcursor(outputFC)

    # keep a dictionary of computed point arrays, to re-use
    computedArrays = dict()
    
    startTime = time.clock()
    totalPoints = int(gp.GetCount_management(inputFC).getoutput(0))
    processedCount = 0
    
    while inRow:
        # get the starting point & its id
        point = inRow.getvalue(inShapeField).getpart()
        id = inRow.getvalue(idField)
        # create the array for storing points
        pointArray = gp.createobject("Array")
        pointArray.add(point)
        dx, dy = 0, 0
        while True:
            samplePt = str(point.x) + " " + str(point.y)
            segId = gp.GetCellValue_management(segIdRaster, samplePt).getoutput(0)
            if segId != "NoData":
                segId = int(segId)
                if segId in computedArrays:
                    arr = computedArrays[segId]
                    for i in range(arr.Count):
                        pointArray.Add(arr.GetObject(i))
                    break
            value = gp.GetCellValue_management(flowDir, samplePt).getoutput(0)
            if value == "NoData":
                pointArray.add(point)
                break
            value = int(value)
            lastDx, lastDy = dx, dy
            if value == 1:
                dx, dy = cellWidth, 0
            elif value == 2:
                dx, dy = cellWidth, -cellHeight
            elif value == 4:
                dx, dy = 0, -cellHeight
            elif value == 8:
                dx, dy = -cellWidth, -cellHeight
            elif value == 16:
                dx, dy = -cellWidth, 0
            elif value == 32:
                dx, dy = -cellWidth, cellHeight
            elif value == 64:
                dx, dy = 0, cellHeight
            elif value == 128:
                dx, dy = cellWidth, cellHeight
            else:
                break
            if (dx != lastDx) or (dy != lastDy):
                pointArray.add(point)
            point.x += dx
            point.y += dy
        computedArrays[id] = pointArray
        # Write the points array to the output feature class
        partsArray = gp.createobject("Array")
        partsArray.add(pointArray)
        outRow = outRows.newrow()
        outRow.setvalue(outShapeField, partsArray)
        outRow.setvalue(idField, id)
        outRows.insertrow(outRow)
        del outRow
        del inRow
        inRow = inRows.next()
        processedCount += 1
        gp.addmessage("Computed " + str(processedCount) + " paths. Approximately " + str(int(((time.clock() - startTime) / processedCount) * (totalPoints - processedCount))) + " seconds remaining.")
    del outRows
    del inRows
except Exception, err:
    gp.AddError(str(err))
    gp.AddError(gp.getmessages(2))