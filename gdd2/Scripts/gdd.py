import arcpy, csv, datetime, httplib, io, json, logging, math, os, re, sqlite3, sys, urllib

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
    arcpy.env.outputCoordinateSystem = sr
    arcpy.env.extent = arcpy.Extent(-20000000, 1800000, -7000000, 11600000)
    arcpy.env.rasterStatistics = 'STATISTICS'
    arcpy.env.overwriteOutput = True
    # Create a scratch geodatabase for storing intermediate results
    root_folder = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
    scratch_folder = os.path.join(root_folder, 'Scratch')
    if not os.path.exists(scratch_folder):
        os.makedirs(scratch_folder)
    scratch_gdb = os.path.join(scratch_folder, 'scratch.gdb')
    if not os.path.exists(scratch_gdb):
        logger.debug('creating scratch.gdb')
        arcpy.management.CreateFileGDB(scratch_folder, 'scratch.gdb')
    arcpy.env.scratchWorkspace = scratch_gdb
    # Create a results geodatabase
    data_folder = os.path.join(root_folder, 'ToolData')
    if not os.path.exists(data_folder):
        os.makedirs(data_folder)
    results_gdb = os.path.join(data_folder, 'data.gdb')
    if not os.path.exists(results_gdb):
        logger.debug('creating data.gdb')
        arcpy.management.CreateFileGDB(data_folder, 'data.gdb')
    arcpy.env.workspace = results_gdb
    # Create a raster catalog in the results geodatabase to store our time series data
    if not arcpy.Exists(_MOSAIC):
        logger.debug('creating %s', _MOSAIC)
        arcpy.management.CreateMosaicDataset(results_gdb, _MOSAIC, sr, 1, '16_BIT_UNSIGNED')
        arcpy.management.AddField(_MOSAIC, 'BeginDate', 'DATE')
        arcpy.management.AddField(_MOSAIC, 'EndDate', 'DATE')
    # Create an sqlite database to hold the temperature station data, and open a connection to it
    temperature_db = os.path.join(scratch_folder, 'temperature.db')
    if not os.path.exists(temperature_db):
        logger.debug('creating temperature.db')
        create_temperature_database(temperature_db)
    global _DBCONN
    _DBCONN = sqlite3.connect(temperature_db)

def create_temperature_database (path):
    '''Create an sqlite database for storing temperature station data. Load the station
id and location information from the Global Historical Climate Network's data inventory file'''
    logger.debug('creating database file and tables')
    db_conn = sqlite3.connect(path)
    db_cursor = db_conn.cursor()
    db_cursor.execute('CREATE TABLE station (id VARCHAR(11) NOT NULL, x INT NOT NULL, y INT NOT NULL, PRIMARY KEY (id));')
    db_cursor.execute('''CREATE TABLE temperature (station VARCHAR(11) NOT NULL REFERENCES station(id), 
                                                   tmin INT NOT NULL, 
                                                   tmax INT NOT NULL, 
                                                   date DATE NOT NULL,
                                                   PRIMARY KEY (station,date));''')
    db_cursor.execute('CREATE INDEX temperature_station_index ON temperature (station);')
    db_cursor.execute('CREATE INDEX temperature_date_index ON temperature (date);')
    db_conn.commit()
    current_year = datetime.datetime.now().year
    stations = []
    logger.debug('loading station data from ncdc.noaa.gov')
    response = http_get('www1.ncdc.noaa.gov', '/pub/data/ghcn/daily/ghcnd-inventory.txt')
    for row in iter(response.splitlines()):
        id = row[0:11].strip()
        lat = float(row[12:20])
        lon = float(row[21:30])
        data = row[31:35].strip()
        first_year = int(row[36:40])
        last_year = int(row[41:45])
        if id[0:2] == 'US' and data == 'TMAX' and last_year == current_year:
            x = 6378137.0 * lon * 0.017453292519943295
            a = lat * 0.017453292519943295
            y = 3189068.5 * math.log((1.0 + math.sin(a)) / (1.0 - math.sin(a)))
            stations.append((id, int(x), int(y),))
    logger.debug('loaded %s stations' % len(stations))
    db_cursor.executemany('INSERT INTO station (id,x,y) VALUES (?, ?, ?)', stations)
    db_conn.commit()
    db_cursor.close()

def store_temperatures (begin_date, end_date):
    '''Download temperature data from National Climate Data Center's Global Historical Climate Network dataset'''
    logger.debug('loading data between %s and %s from ncdc.noaa.gov', begin_date.isoformat(), end_date.isoformat())
    db_cursor = _DBCONN.cursor()
    db_cursor.execute('SELECT id FROM station')
    count = 0
    for record in db_cursor.fetchall():
        station_id = record[0]
        try:
            response = http_get('www1.ncdc.noaa.gov', '/pub/data/ghcn/daily/all/%s.dly' % station_id)
            records = {}
            for row in iter(response.splitlines()):
                element = row[17:21].strip().lower()
                if element != 'tmin' and element != 'tmax': 
                    continue
                year = int(row[11:15])
                month = int(row[15:17])
                end_of_month = datetime.date(year, month, 1) + datetime.timedelta(days=30)
                beginning_of_month = datetime.date(year, month, 1) - datetime.timedelta(days=1)
                if end_of_month < begin_date or beginning_of_month > end_date:
                    continue
                for day,index in enumerate(xrange(21, 262, 8), start=1):
                    observation_date = datetime.date(year, month, day)
                    if observation_date not in records:
                        records[observation_date] = {}
                    if observation_date < begin_date:
                        continue
                    if observation_date > end_date:
                        break
                    celsius_tenths = int(row[index:index+5])
                    if celsius_tenths == -9999:
                        continue
                    fahrenheit = int(celsius_tenths * 0.9/5) + 32
                    records[observation_date][element] = fahrenheit
                db_cursor.executemany('REPLACE INTO temperature (station,date,tmin,tmax) VALUES (?, ?, ?, ?)',
                                      [ (station_id, date, data['tmin'], data['tmax'])
                                        for date, data in records.iteritems()
                                        if data.get('tmin', None) and data.get('tmax', None) ])
                count += db_cursor.rowcount
        except httplib.HTTPException:
            logger.error('error loading data for station %s', station_id)
        _DBCONN.commit()
    db_cursor.close()
    logger.debug('loaded %s observations', count)

def create_gdd_raster (date, min_temp, max_temp):
    '''Create a raster of growing degree days for the given date. Assumes
that temperature data for that date has already been loaded into the
database'''
    logger.debug('creating raster for %s', date.isoformat())
    feature_class = arcpy.management.CreateFeatureclass("in_memory", "temp", "POINT")
    arcpy.management.AddField(feature_class, 'tmin', 'SHORT')
    arcpy.management.AddField(feature_class, 'tmax', 'SHORT')
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
    tmax_ras = arcpy.sa.Idw(feature_class, 'tmax', 5000, 2, arcpy.sa.RadiusVariable(10, 300000))
    tmin_ras = arcpy.sa.Idw(feature_class, 'tmin', 5000, 2, arcpy.sa.RadiusVariable(10, 300000))
    temp_range = max_temp - min_temp
    gdd_ras = arcpy.sa.Minus(arcpy.sa.Divide(arcpy.sa.Plus(tmax_ras, tmin_ras), 2), min_temp)
    gdd_ras = arcpy.sa.Con(gdd_ras < 0, 0, gdd_ras)
    gdd_ras = arcpy.sa.Con(gdd_ras > temp_range, temp_range, gdd_ras)
    prev_day = date - datetime.timedelta(1)
    prev_ras = prev_day.strftime('GDD_%Y%m%d')
    if arcpy.Exists(prev_ras) and (date.month != 1 or date.day != 1):
        gdd_ras = arcpy.sa.Plus(gdd_ras, prev_ras)
    out_ras = date.strftime('GDD_%Y%m%d')
    arcpy.management.CopyRaster(gdd_ras, out_ras, "DEFAULTS", "", 65535, "", "", "16_BIT_UNSIGNED")
    arcpy.management.Delete(feature_class)
    arcpy.management.Delete(gdd_ras)
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
    arcpy.management.AddRastersToMosaicDataset(_MOSAIC, 'Raster Dataset', gdd_img, \
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

def http_get (host, path):
    conn = httplib.HTTPConnection(host)
    conn.request('GET', path)
    response = conn.getresponse()
    if response.status == 200:
        return response.read()
    else:
        raise httplib.HTTPException()

def main (argv=None):
    '''Usage: <script> <begin_date(optional)> <end_date(optional)>
create growing degree day rasters for each day between begin_date 
(which defaults to five days ago) and end_date (which defaults to 
today), inclusive. Dates should be given in YYYY-MM-DD format. Will
only create rasters for days that don't already have one, and only
if at least 3000 temperature observations are available.'''
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
        if db_cursor.fetchone()[0] < 3000:
            logger.debug('insufficient data to create raster for %s' % current_date)
            break
        raster = create_gdd_raster(current_date, 50, 86)
        add_gdd_raster_to_mosaic(raster, current_date)
        current_date = current_date + datetime.timedelta(1)
    db_cursor.close()
    logger.debug('updating mosaic statistics')
    arcpy.management.CalculateStatistics(_MOSAIC)
    arcpy.management.BuildPyramidsandStatistics(_MOSAIC, 'INCLUDE_SUBDIRECTORIES', 'BUILD_PYRAMIDS', 'CALCULATE_STATISTICS')
    arcpy.RefreshCatalog(_MOSAIC)
    return 0

if __name__ == "__main__":
    status = main(sys.argv[1:])
    sys.exit(0)
