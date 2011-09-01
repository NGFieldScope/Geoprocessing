
# Import system modules
import sys, string, os, math, arcgisscripting

# Create the Geoprocessor object
gp = arcgisscripting.create(9.3)
    
input_points = "C:\Documents and Settings\erussell\My Documents\Maps\ESRI_usa\census\cities.sdc\cities"
timeseries_fields = [ "MALES", "FEMALES" ]
neighborhood_size = 8
target_count = 1000
output_points = "C:\\Documents and Settings\\erussell\\My Documents\\Maps\\test_output_2.shp"


def doIt ():
    near_table = r"in_memory\nt"
    gp.generateneartable(input_points, input_points, near_table, "", "", "", "ALL", neighborhood_size)
    near_rows = gp.SearchCursor(near_table)
    near_row = near_rows.Next()
    record_errors = dict()
    record_cache = dict()
    while near_row:
        source_id = near_row.GetValue("IN_FID")
        if source_id in record_cache:
            source_feature = record_cache[source_id]
        else:
            source_feature = gp.SearchCursor(input_points, "OBJECTID = %i" % source_id).Next()
            record_cache[source_id] = source_feature
        if source_id not in record_errors:
            record_errors[source_id] = list()
        target_id = near_row.GetValue("NEAR_FID")
        if target_id in record_cache:
            target_feature = record_cache[target_id]
        else:
            target_feature = gp.SearchCursor(input_points, "OBJECTID = %i" % target_id).Next()
            record_cache[target_id] = target_feature
        for field_name in timeseries_fields:
            diff = source_feature.GetValue(field_name) - target_feature.GetValue(field_name)
            record_errors[source_id].append(math.sqrt(diff * diff))
            source_sum += source_value
        near_row = near_rows.Next()
    
    
    for feature_id in record_errors.iterkeys():
        
        pass
    
    
    
    for feature_id in record_errors.iterkeys():
        errors = record_errors[feature_id]
        record_errors[feature_id] = sum(errors) / len(errors)
    sorted_keys = sorted(record_errors.keys(), key=lambda id: record_errors[id])
    if len(sorted_keys) > target_count:
        gp.CreateFeatureClass(os.path.dirname(output_points), os.path.basename(output_points), "POINT", input_points)
        output_cursor = gp.InsertCursor(output_points)
        for feature_id in sorted_keys[len(sorted_keys)-target_count:]:
            output_cursor.InsertRow(record_cache[feature_id])

doIt()