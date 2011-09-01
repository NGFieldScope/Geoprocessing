'''
Created on Nov 16, 2009

@author: erussell
'''

import sys, string, os, arcgisscripting

try:
    gp = arcgisscripting.create(9.3)
    
    def doIt ():
        gp.Workspace = "C:/Documents and Settings/erussell/My Documents/Maps/interactivemap_regionlayers/regions_proj.gdb"
        for fc in gp.ListFeatureClasses("*"):
            print fc
            for field in gp.describe(fc).Fields:
                if field.AliasName == 'NAME':
                    fieldName = field.Name
            rows = gp.searchcursor(fc)
            row = rows.next()
            while row:
                extent = row.shape.extent
                print '{ "name" : "%s", "extent" : new Extent(%s, %s, %s, %s) }' % (row.getvalue(fieldName), extent.xmin, extent.ymin, extent.xmax, extent.ymax)
                row = rows.next()
    
    doIt()

except Exception, e:
    print e
