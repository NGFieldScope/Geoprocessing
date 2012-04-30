import arcpy, csv, datetime, httplib, io, json, logging, os, re, sqlite3, sys, urllib
from arcpy import env, sa

_DBCONN = None
_MOSAIC = 'growing_degree_days'
logger = logging.getLogger('gdd')

def setup_environment():
    '''Set up the complete execution environment for this script'''
    # Set up basic logging to stdout
    logging.basicConfig(format='%(asctime)s - %(name)s - %(levelname)s - %(message)s', 
                        level=logging.DEBUG)
    # Set up geoprocessing environment defaults
    sr = arcpy.SpatialReference()
    sr.loadFromString(r'PROJCS["WGS_1984_Web_Mercator_Auxiliary_Sphere",GEOGCS["GCS_WGS_1984",DATUM["D_WGS_1984",SPHEROID["WGS_1984",6378137.0,298.257223563]],PRIMEM["Greenwich",0.0],UNIT["Degree",0.0174532925199433]],PROJECTION["Mercator_Auxiliary_Sphere"],PARAMETER["False_Easting",0.0],PARAMETER["False_Northing",0.0],PARAMETER["Central_Meridian",0.0],PARAMETER["Standard_Parallel_1",0.0],PARAMETER["Auxiliary_Sphere_Type",0.0],UNIT["Meter",1.0],AUTHORITY["EPSG",3857]]')
    env.outputCoordinateSystem = sr
    env.extent = arcpy.Extent(-20000000, 1800000, -7000000, 11600000)
    env.rasterStatistics = 'STATISTICS'
    env.overwriteOutput = True
    # Create a scratch geodatabase for storing intermediate results
    root_folder = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
    scratch_folder = os.path.join(root_folder, 'Scratch')
    scratch_gdb = os.path.join(scratch_folder, 'scratch.gdb')
    if not os.path.exists(scratch_gdb):
        logger.debug('creating scratch.gdb')
        arcpy.CreateFileGDB_management(scratch_folder, 'scratch.gdb')
    env.scratchWorkspace = scratch_gdb
    # Create a results geodatabase
    data_folder = os.path.join(root_folder, 'ToolData')
    results_gdb = os.path.join(data_folder, 'data.gdb')
    if not os.path.exists(results_gdb):
        logger.debug('creating data.gdb')
        arcpy.CreateFileGDB_management(data_folder, 'data.gdb')
    env.workspace = results_gdb
    # Create a raster catalog in the results geodatabase to store our time series data
    if not arcpy.Exists(_MOSAIC):
        logger.debug('creating %s', _MOSAIC)
        arcpy.CreateMosaicDataset_management(results_gdb, _MOSAIC, sr, 1, '16_BIT_UNSIGNED')
        arcpy.AddField_management(_MOSAIC, 'BeginDate', 'DATE')
        arcpy.AddField_management(_MOSAIC, 'EndDate', 'DATE')
    # Create an sqlite database to hold the temperature station data, and open a connection to it
    temperature_db = os.path.join(scratch_folder, 'temperature.db')
    if not os.path.exists(temperature_db):
        logger.debug('creating temperature.db')
        create_temperature_database(temperature_db)
    global _DBCONN
    _DBCONN = sqlite3.connect(temperature_db)

def create_temperature_database (path):
    '''Create an sqlite database for storing temperature station data. Load the station
id and location information from the NCDC's ArcGIS Server REST web service'''
    logger.debug('creating database file and tables')
    db_conn = sqlite3.connect(path)
    db_cursor = db_conn.cursor()
    db_cursor.execute('CREATE TABLE station (id VARCHAR(11) NOT NULL, name VARCHAR(64), x INT NOT NULL, y INT NOT NULL, PRIMARY KEY (id));')
    db_cursor.execute('CREATE TABLE temperature (station VARCHAR(11) NOT NULL REFERENCES station(id), tmin INT NOT NULL, tmax INT NOT NULL, date DATE NOT NULL, PRIMARY KEY (station,date));')
    db_cursor.execute('CREATE INDEX temperature_station_index ON temperature (station);')
    db_cursor.execute('CREATE INDEX temperature_date_index ON temperature (date);')
    db_conn.commit()
    day_before_yesterday = datetime.date.today() - datetime.timedelta(2)
    logger.debug('loading list of station ids from web service')
    params = { 'where' : "COUNTRY='US' AND END_DATE>='%s'" % day_before_yesterday.strftime('%Y/%m/%d'),
               'returnIdsOnly': 'true',
               'f': 'json' }
    response = read_post_response('gis.ncdc.noaa.gov', '/rest/services/cdo/gsod/MapServer/0/query', params)
    objectids = json.loads(response)['objectIds']
    stations = []
    start_index = 0
    logger.debug('loading data for %s stations' % len(objectids))
    while start_index < len(objectids):
        end_index = min(start_index + 200, len(objectids))
        params = { 'where' : 'OBJECTID IN (%s)' % ','.join(map(str, objectids[start_index:end_index])),
                   'outSR': '102100',
                   'outFields': 'AWSBAN,STATION',
                   'f': 'json' }
        response = read_post_response('gis.ncdc.noaa.gov', '/rest/services/cdo/gsod/MapServer/0/query', params)
        for record in json.loads(response)['features']:
            stations.append((str(record['attributes']['AWSBAN']),
                             str(record['attributes']['STATION']),
                             int(record['geometry']['x']),
                             int(record['geometry']['y']),))
        start_index += 200
    logger.debug('inserting stations into database')
    db_cursor.executemany('INSERT INTO station (id,name,x,y) VALUES (?, ?, ?, ?)', stations)
    db_conn.commit()
    db_cursor.close()

def store_temperatures (begin_date, end_date):
    '''Download temperature data from National Climate Data Center's Global Summary of Day dataset'''
    logger.debug('loading data from %s to %s from ncdc.noaa.gov', begin_date.isoformat(), end_date.isoformat())
    db_cursor = _DBCONN.cursor()
    db_cursor.execute("SELECT s.id FROM station s")
    stations = [ record[0] for record in db_cursor.fetchall() ]
    params = { 'p_ndatasetid' : 10, 'datasetabbv' : 'GSOD', 'p_cqueryby' : 'ENTIRE',
               'p_csubqueryby' : '', 'p_nrgnid' : '', 'p_ncntryid' : '', 'p_nstprovid' : '',
               'volume' : 0, 'datequerytype' : 'RANGE', 'outform' : 'COMMADEL', 
               'startYear' : begin_date.year,
               'startMonth' : '%02d' % begin_date.month,
               'startDay' : '%02d' % begin_date.day,
               'endYear' : end_date.year,
               'endMonth' : '%02d' % end_date.month,
               'endDay' : '%02d' % end_date.day,
               'p_asubqueryitems' : stations }
    result_page = read_post_response('www7.ncdc.noaa.gov', '/CDO/cdodata.cmd', params)
    records = []
    match = re.search('<p><a href="(http://www\d\.ncdc\.noaa\.gov/pub/orders/CDO\d+\.txt)">CDO\d+\.txt</a></p>', result_page, re.MULTILINE)
    csv_data = urllib.urlopen(match.group(1))
    for i, row in enumerate(csv.reader(csv_data)):
        if i == 0: continue # skip headers
        tmax = int(round(float(row[17][:-1])))
        if tmax == 10000: continue
        tmin = int(round(float(row[18][:-1])))
        if tmin == 10000: continue
        id = row[0] + row[1]
        date = datetime.datetime.strptime(row[2].strip(), '%Y%m%d').date()
        records.append((id, tmax, tmin, date,))
    logger.debug('loaded %s observations', len(records))
    db_cursor.executemany('REPLACE INTO temperature (station,tmax,tmin,date) VALUES (?, ?, ?, ?)', records)
    _DBCONN.commit()
    db_cursor.close()

def create_gdd_raster (date, min_temp, max_temp):
    '''Create a raster of growing degree days for the given date. Assumes
that temperature data for that date has already been loaded into the
database'''
    logger.debug('creating raster for %s', date.isoformat())
    feature_class = arcpy.CreateFeatureclass_management("in_memory", "temp", "POINT")
    arcpy.AddField_management(feature_class, 'tmin', 'SHORT')
    arcpy.AddField_management(feature_class, 'tmax', 'SHORT')
    fc_cursor = arcpy.InsertCursor(feature_class)
    point = arcpy.Point()
    db_cursor = _DBCONN.cursor()
    db_cursor.execute('SELECT s.x,s.y,t.tmax,t.tmin FROM temperature t INNER JOIN station s ON s.id=t.station WHERE t.date=?', (date,))
    rcount = 0
    for record in db_cursor.fetchall():
        point.X = record[0]
        point.Y = record[1]
        row = fc_cursor.newRow()
        row.shape = point
        row.tmax = record[2]
        row.tmin = record[3]
        fc_cursor.insertRow(row)
        rcount += 1
    db_cursor.close()
    del fc_cursor
    logger.debug('interpolating %s points', rcount)
    arcpy.CheckOutExtension("Spatial")
    tmax_ras = sa.Idw(feature_class, 'tmax', 5000, 2, sa.RadiusVariable(10, 300000))
    tmin_ras = sa.Idw(feature_class, 'tmin', 5000, 2, sa.RadiusVariable(10, 300000))
    temp_range = max_temp - min_temp
    gdd_ras = sa.Minus(sa.Divide(sa.Plus(tmax_ras, tmin_ras), 2), min_temp)
    gdd_ras = sa.Con(gdd_ras < 0, 0, gdd_ras)
    gdd_ras = sa.Con(gdd_ras > temp_range, temp_range, gdd_ras)
    prev_day = date - datetime.timedelta(1)
    prev_ras = prev_day.strftime('GDD_%Y%m%d')
    if arcpy.Exists(prev_ras) and (date.month != 1 or date.day != 1):
        gdd_ras = sa.Plus(gdd_ras, prev_ras)
    out_ras = date.strftime('GDD_%Y%m%d')
    arcpy.CopyRaster_management(gdd_ras, out_ras, "DEFAULTS", "", 65535, "", "", "16_BIT_UNSIGNED")
    arcpy.Delete_management(feature_class)
    arcpy.Delete_management(gdd_ras)
    arcpy.CheckInExtension("Spatial")
    return out_ras

def add_gdd_raster_to_mosaic (gdd_img, date):
    '''Add the given growing degree day raster for the given date to the master
raster catalog, and mark it as beloning to that date'''
    rows = arcpy.UpdateCursor(_MOSAIC, "Name = '%s'" % gdd_img)
    for row in rows:
        logger.debug('removing existing raster %s', gdd_img)
        rows.deleteRow(row)
    del rows
    logger.debug('adding raster %s to mosaic', gdd_img)
    arcpy.AddRastersToMosaicDataset_management(_MOSAIC, 'Raster Dataset', gdd_img, \
                                               'UPDATE_CELL_SIZES', 'UPDATE_BOUNDARY', 'NO_OVERVIEWS', \
                                               '#', '#', '#', '#', '#', '#', '#', \
                                               'BUILD_PYRAMIDS', 'CALCULATE_STATISTICS', 'BUILD_THUMBNAILS')
    rows = arcpy.UpdateCursor(_MOSAIC, "Name = '%s'" % gdd_img)
    end_date = date + datetime.timedelta(1)
    for row in rows:
        row.BeginDate = "%s/%s/%s" % (date.month, date.day, date.year,)
        row.EndDate = "%s/%s/%s" % (end_date.month, end_date.day, end_date.year,)
        rows.updateRow(row)
    del rows

def read_post_response (host, path, param_dict):
    params = urllib.urlencode(param_dict, True)
    headers = {"Content-type": "application/x-www-form-urlencoded"}
    conn = httplib.HTTPConnection(host)
    conn.request('POST', path, params, headers)
    response = conn.getresponse()
    if response.status == 200:
        return response.read()
    else:
        raise httplib.HTTPException()

def main (argv=None):
    '''Usage: <script> <begin_date(optional)> <end_date(optional)>
create growing degree day rasters for each day between begin_date 
(which defaults to seven days ago) and end_date (which defaults to 
today), inclusive. Dates should be given in YYYY-MM-DD format. Will
only create rasters for days that don't already have one, and only
if at least 1500 temperature observations are available.'''
    setup_environment()
    begin_date = datetime.date.today() - datetime.timedelta(5)
    end_date = datetime.date.today()
    if argv is not None and len(argv) > 0:
        begin_date = datetime.datetime.strptime(argv[0], '%Y-%m-%d').date()
    if argv is not None and len(argv) > 1:
        end_date = datetime.datetime.strptime(argv[1], '%Y-%m-%d').date()
    if end_date < begin_date:
        raise Exception('begin_date must be before end_date')
    if begin_date.month != 1 or begin_date.day != 1:
        prev_date = begin_date - datetime.timedelta(1)
        if not arcpy.Exists(prev_date.strftime('GDD_%Y%m%d')):
            raise Exception('beginning date %s is not the first day of year and previous day has no data' % begin_date.isoformat())
    logger.info('running gdd script for %s to %s' % (begin_date.isoformat(), end_date.isoformat()))
    while arcpy.Exists(begin_date.strftime('GDD_%Y%m%d')) and begin_date <= end_date:
        logger.debug('raster already exists for %s' % begin_date)
        begin_date = begin_date + datetime.timedelta(1)
    if begin_date > end_date:
        return 0
    store_temperatures(begin_date, end_date)
    db_cursor = _DBCONN.cursor()
    current_date = begin_date
    while current_date <= end_date:
        db_cursor.execute('SELECT COUNT (*) from temperature t WHERE t.date=?', (current_date,))
        if db_cursor.fetchone()[0] < 1500:
            logger.debug('insufficient data to create raster for %s' % current_date)
            break
        raster = create_gdd_raster(current_date, 50, 86)
        add_gdd_raster_to_mosaic(raster, current_date)
        current_date = current_date + datetime.timedelta(1)
    db_cursor.close()
    logger.debug('updating mosaic statistics')
    arcpy.CalculateStatistics_management(_MOSAIC)
    arcpy.BuildPyramidsandStatistics_management(_MOSAIC, 'INCLUDE_SUBDIRECTORIES', 'BUILD_PYRAMIDS', 'CALCULATE_STATISTICS')
    arcpy.RefreshCatalog(_MOSAIC)
    return 0

if __name__ == "__main__":
    status = main(sys.argv[1:])
    sys.exit(0)
    
