'''
Created on Nov 16, 2009

@author: erussell
'''

import sys, string, os, arcgisscripting

try:
    gp = arcgisscripting.create(9.3)
    
    def doIt ():
        gp.Workspace = "C:/Documents and Settings/erussell/My Documents/Maps/interactivemap_regionlayers/regions_proj.gdb"
        fc = "country_SpatialJoin"
        rows = gp.searchcursor(fc)
        row = rows.next()
        while row:
            extent = row.shape.extent
            print '"%s", "%s", "new Extent(%s, %s, %s, %s)"' % (row.getvalue('CONTINENT'), row.getvalue('CNTRY_NAME'), extent.xmin, extent.ymin, extent.xmax, extent.ymax)
            row = rows.next()
    
    doIt()

except Exception, e:
    print e
