import json, logging, math, os, string, sys, urllib, urllib2

mapservice_url = "http://fieldscope/ArcGIS/rest/services/budburst/surface_temp/MapServer"
destination = "C:/Users/Administrator/Documents/UTFGrid/st_web"

class hashabledict(dict):
  def __key(self):
    return tuple((k,self[k]) for k in sorted(self))
  def __hash__(self):
    return hash(self.__key())
  def __eq__(self, other):
    return self.__key() == other.__key()

def encode_char (index):
    index += 32
    if index >= 34:
        index += 1
    if index >= 92:
        index += 1
    return unichr(index)

def meters_to_tile (level, mx, my):
    resolution = (2 * math.pi * 6378137 / 256) / 2**level
    origin = 2 * math.pi * 6378137 / 2.0
    px = (mx + origin) / resolution
    py = (origin - my) / resolution
    tx = int(math.floor(px / 256.0 ))
    ty = int(math.floor(py / 256.0))
    return tx,ty

def tiles (level, xmin, ymin, xmax, ymax):
    resolution = (2 * math.pi * 6378137 / 256) / 2**level
    origin = 2 * math.pi * 6378137 / 2.0
    tile_size = 256 * resolution
    x1,y1 = meters_to_tile(level, xmin, ymax)
    x2,y2 = meters_to_tile(level, xmax, ymin)
    for x in xrange(x1, x2+1):
        for y in xrange(y1, y2+1):
            mx = -origin + tile_size * x
            my = origin - tile_size * y
            yield ((x,y,),(mx, my - tile_size, mx + tile_size, my,),)

def collect_data (service, bbox, size, fields):
    w = (bbox[2] - bbox[0]) / (size - 1)
    h = (bbox[3] - bbox[1]) / (size - 1)
    data_cells = {}
    for x in xrange(size):
        for y in xrange(size):
            area = (bbox[0] + x * w, bbox[3] - y * h, bbox[0] + (x + 1) * w, bbox[3] - (y + 1) * h,)
            value = query_service(service, bbox, area, size, fields)
            if value in data_cells:
                data_cells[value].append((x,y,))
            else:
                data_cells[value] = [(x,y,),]
    grid = [[u" "] * size for i in xrange(size) ]
    keys = [ u"",]
    data = {}
    key_index = 0
    for value,cells in data_cells.items():
        if len(value) > 0:
            char = encode_char(len(keys))
            key = str(key_index)
            for cell in cells:
                grid[cell[1]][cell[0]] = char
            keys.append(key)
            data[key] = value
            key_index += 1
    return { "grid" : [ u"".join(row) for row in grid ],
             "keys" : keys,
             "data" : data }

def query_service (service, bounds, area, size, fields):
    data = {
        "geometryType": "esriGeometryEnvelope",
        "geometry": '{"xmin": %f, "ymin": %f, "xmax": %f, "ymax": %f}' % area,
        "mapExtent": "%f, %f, %f, %f" % bounds,
        "tolerance": "0",
        "layers": "all",
        "imageDisplay": "%d,%d,96" % (size * 2, size * 2),
        "returnGeometry": "false",
        "f": "json",
    }
    response = json.loads(urllib2.urlopen(service + "/identify", urllib.urlencode(data)).read())
    result = hashabledict()
    for layer_result in response['results']:
        for key,value in layer_result['attributes'].items():
            if key in fields:
                result[key] = value
    return result

def main (argv=None):
    # Set up basic logging to stdout
    logging.basicConfig(format='%(asctime)s - %(name)s - %(levelname)s - %(message)s', 
                        level=logging.DEBUG)
    if not os.path.exists(destination):
        os.makedirs(destination)
    config = json.loads(urllib2.urlopen(mapservice_url + "?f=json").read())
    wkid = config['spatialReference']['wkid']
    if not (wkid == 102113 or wkid == 102100):
        raise Exception('Map service must be in Web Mercator projection')
    tile_size = (256,256,)
    full_extent = (config['fullExtent']['xmin'], config['fullExtent']['ymin'],
                   config['fullExtent']['xmax'], config['fullExtent']['ymax'],)
    for level in xrange(0, 19):
        for tile,bbox in tiles(level, *full_extent):
            folder = os.path.join(os.path.join(destination, str(level)), str(tile[0]))
            if not os.path.exists(folder):
                os.makedirs(folder)
            file_path = os.path.join(folder, str(tile[1]) + ".json")
            if not os.path.exists(file_path):
                data = collect_data(mapservice_url, bbox, 128, ['Class'])
                with open(file_path, "w") as file:
                    file.write(json.dumps(data, indent=2))

if __name__ == "__main__":
    status = main(sys.argv[1:])
    sys.exit(0)
