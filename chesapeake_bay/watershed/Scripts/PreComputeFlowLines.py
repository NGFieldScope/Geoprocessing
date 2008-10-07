# ---------------------------------------------------------------------------
#
# ---------------------------------------------------------------------------

# E:/GIS/cb_gp_watershed/test_point_1.shp "Id" E:/GIS/cb_gp_watershed/data.gdb/test_line_1

# Import system modules
import sys, string, os, arcgisscripting

# Create the Geoprocessor object
gp = arcgisscripting.create(9.3)
gp.overwriteoutput = 1

try:
    gp.toolbox = "management"
    
    # Script arguments...
    inputFC = sys.argv[1]
    idField = sys.argv[2]
    flowDir = sys.argv[3]
    outputFC = sys.argv[4]
    
    # Read points from the input feature class
    inShapeField = gp.describe(inputFC).ShapeFieldName
    inRows = gp.searchcursor(inputFC)
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
        outputSR.CreateFromFile("C:/Program Files/ArcGIS/Coordinate Systems/Geographic Coordinate Systems/North America/North American Datum 1983.prj")
        gp.CreateFeatureclass_management(os.path.split(outputFC)[0], os.path.split(outputFC)[1], "POLYLINE", "#", "DISABLED", "DISABLED", outputSR)
        gp.addfield(outputFC, idField, "LONG")
    
    # create cursor for writing to the output feature class
    outShapeField = gp.describe(outputFC).ShapeFieldName
    outRows = gp.insertcursor(outputFC)
    
    while inRow:
        # get the starting point & its id
        point = inRow.getvalue(inShapeField).getpart()
        id = inRow.getvalue(idField)
        gp.addmessage("Computing path " + str(id ))
        # create the array for storing points
        pointArray = gp.createobject("Array")
        pointArray.add(point)
        try:
            dx, dy = 0, 0
            while True:
                samplePt = str(point.x) + " " + str(point.y)
                value = int(gp.GetCellValue_management(flowDir, samplePt).getoutput(0))
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
        except Exception, err:
            pointArray.add(point)
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
    del outRows
    del inRows
except Exception, err:
    gp.AddError(str(err))
    gp.AddError(gp.getmessages(2))