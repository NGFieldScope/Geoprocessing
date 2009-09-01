# ---------------------------------------------------------------------------
# flow_path.py
# Usage: flow_path <Point> <FeatureClass> 
# ---------------------------------------------------------------------------

# E:/GIS/cb_gp_watershed/test_point_1.shp E:/GIS/cb_gp_watershed/line_out.shp

# Import system modules
import time, sys, string, os, arcgisscripting

# Create the Geoprocessor object
gp = arcgisscripting.create(9.3)
gp.overwriteoutput = 1

try:
    toolShareFolder = os.path.dirname(sys.path[0]) + os.path.sep
    toolDataFolder = toolShareFolder + "ToolData" + os.path.sep
    toolDataGDB = toolDataFolder + "ToolData.gdb" + os.path.sep
    
    # Script arguments...
    inputFC = sys.argv[1]
    outputFC = sys.argv[2]

    # Read the first point from the input feature class    
    rows = gp.searchcursor(inputFC)
    row = rows.next()
    point = row.getvalue(gp.describe(inputFC).ShapeFieldName).getpart()

    # create the output feature class if it doesn't exist already
    try:
        gp.describe(outputFC)
    except Exception, err:
        outputSR = gp.CreateObject("spatialreference")
        outputSR.CreateFromFile(toolDataFolder + "OutputSpatialReference.prj")
        gp.CreateFeatureclass_management(os.path.split(outputFC)[0], os.path.split(outputFC)[1], "POLYLINE", "#", "DISABLED", "DISABLED", outputSR)
    
    # get the cell size of the flow direction raster
    flowDirDataset = toolDataFolder + "flow_composite_500_30sec.img"
    flowDesc = gp.describe(flowDirDataset + "/Band_1")
    cellWidth = flowDesc.MeanCellWidth
    cellHeight = flowDesc.MeanCellHeight
    # segments dataset
    segmentsFC = toolDataGDB + "precomputed_segments_500"
    segmentsFieldName = gp.describe(segmentsFC).ShapeFieldName

    # create the array for storing points
    pointArray = gp.createobject("Array")
    pointArray.add(point)
    # keep track of how many points we've output and how many steps we've made
    stepCount = 0
    pointCount = 2
    segmentId = 0
    dx, dy = 0, 0
    startTime = time.clock()
    while True:
        samplePt = str(point.x) + " " + str(point.y)
        values = gp.GetCellValue_management(flowDirDataset, samplePt).getoutput(0).split("\\n")
        flowDir = values[0]
        if (flowDir == "NoData"):
            pointArray.add(point)
            break
        segmentId = int(values[1])
        if (segmentId != 0):
            break
        flowDir = int(flowDir)
        lastDx, lastDy = dx, dy
        if flowDir == 1:
            dx, dy = cellWidth, 0
        elif flowDir == 2:
            dx, dy = cellWidth, -cellHeight
        elif flowDir == 4:
            dx, dy = 0, -cellHeight
        elif flowDir == 8:
            dx, dy = -cellWidth, -cellHeight
        elif flowDir == 16:
            dx, dy = -cellWidth, 0
        elif flowDir == 32:
            dx, dy = -cellWidth, cellHeight
        elif flowDir == 64:
            dx, dy = 0, cellHeight
        elif flowDir == 128:
            dx, dy = cellWidth, cellHeight
        else:
            break
        if (dx != lastDx) or (dy != lastDy):
            pointArray.add(point)
            pointCount += 1
        point.x += dx
        point.y += dy
        stepCount += 1
    gp.addmessage("Flow time=" + str(time.clock() - startTime) + " seconds")
    startTime = time.clock()
    if (segmentId != 0):
        segments = gp.searchcursor(segmentsFC, "POINTID = " + str(segmentId))
        segment = segments.next()
        if (segment):
            segmentPoints = segment.getvalue(segmentsFieldName).getpart().getobject(0)
            point = segmentPoints.next()
            while (point):
                pointArray.add(point)
                point = segmentPoints.next()
            del segment
        del segments
        gp.addmessage("Segment_Id=" + str(segmentId))
    gp.addmessage("Segment time=" + str(time.clock() - startTime) + " seconds")
    # Write the points array to the output feature class
    partsArray = gp.createobject("Array")
    partsArray.add(pointArray)
    rows = gp.insertcursor(outputFC)
    row = rows.newrow()
    row.setvalue(gp.describe(outputFC).ShapeFieldName, partsArray)
    rows.insertrow(row)
    del rows
    del row
    gp.addmessage("Total steps=" + str(stepCount))
    gp.addmessage("Total points=" + str(pointCount))
except Exception, err:
    gp.AddError(str(err))
    gp.AddError(gp.getmessages(2))
