import arcpy, csv, datetime, httplib, io, logging, os, re, sqlite3, sys, urllib
from arcpy import env, sa

_DBCONN = None
_MOSAIC = 'growing_degree_days'
_YESTERDAY = None
logger = logging.getLogger('gdd')

def create_database (path):
    '''Create an sqlite database for storing temperature station data. Load the station
id and location information from the stations.db file, which should be distributed with
this script'''
    dbconn = sqlite3.connect('stations.db')
    dbcurs = dbconn.cursor()
    dbcurs.execute('SELECT * FROM station WHERE 1=1')
    stations = list(dbcurs.fetchall())
    dbcurs.close()
    dbconn = sqlite3.connect(path)
    dbcurs = dbconn.cursor()
    dbcurs.execute('CREATE TABLE station (id VARCHAR(11) NOT NULL, source VARCHAR(5), name VARCHAR(64), easting INT NOT NULL, northing INT NOT NULL, elevation DOUBLE(6,1), gsod_daily BOOLEAN, PRIMARY KEY (id));')
    dbcurs.execute('CREATE INDEX gsod_daily_index ON station (gsod_daily);')
    dbcurs.execute('CREATE TABLE temperature (station VARCHAR(11) NOT NULL, tmin INT NOT NULL, tmax INT NOT NULL, date DATE NOT NULL, PRIMARY KEY (station,date));')
    dbcurs.execute('CREATE INDEX temperature_station_index ON temperature (station);')
    dbcurs.execute('CREATE INDEX temperature_date_index ON temperature (date);')
    dbconn.commit()
    dbcurs.executemany('INSERT INTO station (id,source,name,easting,northing,elevation,gsod_daily) VALUES (?, ?, ?, ?, ?, ?, ?)', stations)
    dbconn.commit()
    dbcurs.close()

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
    # Create a scratch geodatabase for storing intermediate results
    folder = os.path.dirname(os.path.abspath(__file__))
    scratch_gdb = os.path.join(folder, 'scratch.gdb')
    if not os.path.exists(scratch_gdb):
        logger.debug('creating scratch.gdb')
        arcpy.CreateFileGDB_management(folder, 'scratch.gdb')
    env.scratchWorkspace = scratch_gdb
    # Create a results geodatabase
    results_gdb = os.path.join(folder, 'data.gdb')
    if not os.path.exists(results_gdb):
        logger.debug('creating data.gdb')
        arcpy.CreateFileGDB_management(folder, 'data.gdb')
    env.workspace = results_gdb
    # Create a raster catalog in the results geodatabase to store our time series data
    if not arcpy.Exists(_MOSAIC):
        logger.debug('creating %s', _MOSAIC)
        arcpy.CreateMosaicDataset_management(results_gdb, _MOSAIC, sr, 1, '16_BIT_UNSIGNED')
        arcpy.AddField_management(_MOSAIC, 'BeginDate', 'DATE')
        arcpy.AddField_management(_MOSAIC, 'EndDate', 'DATE')
    # Create an sqlite database to hold the temperature station data, and open a connection to it
    db = os.path.join(folder, 'temperature.db')
    if not os.path.exists(db):
        logger.debug('creating temperature.db')
        create_database(db)
    global _DBCONN
    _DBCONN = sqlite3.connect(db)

def get_daily_data ():
    '''Download temperature data from NOAA's Climate Prediction Center. Returns a tuple of the date and the data.'''
    result = []
    data = urllib.urlopen('http://www.cpc.ncep.noaa.gov/products/analysis_monitoring/cdus/prcp_temp_tables/dly_glob1.txt')
    for i, row in enumerate(data):
        if i == 21:
            date = datetime.datetime.strptime(row.strip(), '%Y%m%d').date()
            continue
        if i < 22: continue # skip headers
        tmax = int(row[0:4])
        if tmax == -999: continue # skip missing data
        tmin = int(row[4:8])
        if tmin == -999: continue
        id = row[28:33]
        result.append((id, tmax, tmin, date,))
    logger.debug('loaded %s points for %s from cpc.ncep.noaa.gov', len(result), date.isoformat())
    return (date, result,)

def get_gsod_data (begin_date, end_date, stations=None):
    '''Download temperature data from National Climate Data Center's Global Summary of Day dataset'''
    if stations is None:
        cursor = _DBCONN.cursor()
        cursor.execute("SELECT s.id FROM station s WHERE s.source='GSOD'")
        stations = [ record[0] for record in cursor.fetchall() ]
        cursor.close()
    param_dict = { 'p_ndatasetid' : 10, 'datasetabbv' : 'GSOD', 'p_cqueryby' : 'ENTIRE',
                   'p_csubqueryby' : '', 'p_nrgnid' : '', 'p_ncntryid' : '', 'p_nstprovid' : '',
                   'volume' : 0, 'datequerytype' : 'RANGE', 'outform' : 'COMMADEL', 
                   'startYear' : begin_date.year,
                   'startMonth' : '%02d' % begin_date.month,
                   'startDay' : '%02d' % begin_date.day,
                   'endYear' : end_date.year,
                   'endMonth' : '%02d' % end_date.month,
                   'endDay' : '%02d' % end_date.day,
                   'p_asubqueryitems' : stations }
    params = urllib.urlencode(param_dict, True)
    headers = {"Content-type": "application/x-www-form-urlencoded"}
    conn = httplib.HTTPConnection('www7.ncdc.noaa.gov')
    conn.request('POST', '/CDO/cdodata.cmd', params, headers)
    response = conn.getresponse()
    result = []
    if response.status == 200:
        result_page = response.read()
        match = re.search('<p><a href="(http://www\d\.ncdc\.noaa\.gov/pub/orders/CDO\d+\.txt)">CDO\d+\.txt</a></p>', result_page, re.MULTILINE)
        data = urllib.urlopen(match.group(1))
        for i, row in enumerate(csv.reader(data)):
            if i == 0: continue # skip headers
            tmax = int(round(float(row[17][:-1])))
            if tmax == 10000: continue
            tmin = int(round(float(row[18][:-1])))
            if tmin == 10000: continue
            id = row[0] + row[1]
            date = datetime.datetime.strptime(row[2].strip(), '%Y%m%d').date()
            result.append((id, tmax, tmin, date,))
        logger.debug('loaded %s points for %s to %s from ncdc.noaa.gov', len(result), begin_date.isoformat(), end_date.isoformat())
    else:
        logger.error('http response code %s from download request to ncdc.noaa.gov', response.status)
    return result

def store_temperatures (date):
    '''Store temperatures for the given date in the sqlite database. Uses CPC 
and GSOD data if the requested day is available from the CPC, otherwise uses 
only GSOD data using additional stations to replace the CPC data'''
    sql = _DBCONN.cursor()
    sql.execute('SELECT COUNT(*) FROM temperature t WHERE t.date=?', (date,))
    tcount = sql.fetchall()[0][0]
    if tcount == 0:
        logger.debug('downloading data for %s', date.isoformat())
        global _YESTERDAY
        ddate = None
        if _YESTERDAY is None or date == _YESTERDAY:
            ddate, data = get_daily_data()
            if _YESTERDAY is None:
                _YESTERDAY = ddate
        if ddate == date:
            sql.execute("SELECT id FROM station WHERE gsod_daily=1")
            gsod_stations = [ record[0] for record in sql.fetchall() ]
            data.extend(get_gsod_data(date, date, gsod_stations))
        else:
            data = get_gsod_data(date, date)
        sql.executemany('INSERT INTO temperature (station,tmax,tmin,date) VALUES (?, ?, ?, ?)', data)
        _DBCONN.commit()
    sql.close()

def create_gdd_raster (date):
    '''Create a raster of growing degree days for the given date. Assumes
that temperature data for that date has already been loaded into the
database'''
    logger.debug('creating raster for %s', date.isoformat())
    feature_class = arcpy.CreateFeatureclass_management("in_memory", "temp", "POINT")
    arcpy.AddField_management(feature_class, 'tmin', 'SHORT')
    arcpy.AddField_management(feature_class, 'tmax', 'SHORT')
    cursor = arcpy.InsertCursor(feature_class)
    point = arcpy.Point()
    sql = _DBCONN.cursor()
    sql.execute('SELECT s.easting,s.northing,t.tmax,t.tmin FROM temperature t INNER JOIN station s ON s.id=t.station WHERE t.date=?', (date,))
    rcount = 0
    for record in sql.fetchall():
        point.X = record[0]
        point.Y = record[1]
        row = cursor.newRow()
        row.shape = point
        row.tmax = record[2]
        row.tmin = record[3]
        cursor.insertRow(row)
        rcount += 1
    del cursor
    logger.debug('interpolating %s points', rcount)
    arcpy.CheckOutExtension("Spatial")
    tmax_ras = sa.Idw(feature_class, 'tmax', 5000, 2, sa.RadiusVariable(10, 300000))
    tmin_ras = sa.Idw(feature_class, 'tmin', 5000, 2, sa.RadiusVariable(10, 300000))
    gdd_ras = sa.Minus(sa.Divide(sa.Plus(tmax_ras, tmin_ras), 2), 50)
    gdd_ras = sa.Con(gdd_ras < 0, 0, gdd_ras)
    gdd_ras = sa.Con(gdd_ras > 36, 36, gdd_ras)
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
    del row
    del rows

def main (argv=None):
    '''Usage: <script> <begin_date(optional)> <end_date(optional)>
create growing degree day rasters for each day between begin_date 
(which defaults to yesterday) and end_date (which defaults to 
begin_date), inclusive. Dates should be given in YYYY-MM-DD format.'''
    setup_environment()
    begin_date = datetime.date.today() - datetime.timedelta(1)
    if argv is not None and len(argv) > 0:
        begin_date = datetime.datetime.strptime(argv[0], '%Y-%m-%d').date()
    end_date = begin_date
    if argv is not None and len(argv) > 1:
        end_date = datetime.datetime.strptime(argv[1], '%Y-%m-%d').date()
    if end_date < begin_date:
        sys.stderr.write("begin_date must be before end_date\n")
        return -1
    date = begin_date
    while date <= end_date:
        store_temperatures(date)
        raster = create_gdd_raster(date)
        add_gdd_raster_to_mosaic(raster, date)
        date = date + datetime.timedelta(1)
    logger.debug('updating mosaic statistics')
    arcpy.CalculateStatistics_management(_MOSAIC)
    arcpy.BuildPyramidsandStatistics_management(_MOSAIC, 'INCLUDE_SUBDIRECTORIES', 'BUILD_PYRAMIDS', 'CALCULATE_STATISTICS')
    arcpy.RefreshCatalog(_MOSAIC)
    return 0

if __name__ == "__main__":
    status = main(sys.argv[1:])
    sys.exit(status)
    
