import java.io.IOException;
import java.util.Arrays;
import ucar.ma2.Array;
import ucar.ma2.ArrayDouble;
import ucar.ma2.ArrayFloat;
import ucar.ma2.ArrayShort;
import ucar.ma2.DataType;
import ucar.ma2.Index;
import ucar.ma2.Index3D;
import ucar.ma2.InvalidRangeException;
import ucar.nc2.Dimension;
import ucar.nc2.NetcdfFileWriteable;
import ucar.nc2.Variable;
import ucar.nc2.dataset.CoordinateAxis;
import ucar.nc2.dataset.CoordinateAxis1DTime;
import ucar.nc2.dt.GridCoordSystem;
import ucar.nc2.dt.GridDataset;
import ucar.nc2.dt.GridDatatype;


public class ComputeGDD 
{
    private static GridDataset.Gridset getTimeGrid (GridDataset in) {
        for (GridDataset.Gridset grids : in.getGridsets()) {
            if (grids.getGeoCoordSystem().getTimeAxis1D() != null) {
                return grids;
            }
        }
        return null;
    }
    
    private static void setupDataset (NetcdfFileWriteable out) {
        
        // Global attributes
        
        out.addGlobalAttribute("Conventions", "CF-1.0");
        out.addGlobalAttribute("standardpar2", new Double(50.000001));
        out.addGlobalAttribute("centerlon", new Double(-107.0));
        out.addGlobalAttribute("history", "created by National Geographic Education Programs");
        out.addGlobalAttribute("institution", "National Geographic Society");
        ArrayFloat.D1 latcorners = new ArrayFloat.D1(4);
        latcorners.set(0, 1.0f);
        latcorners.set(1, 0.897945f);
        latcorners.set(2, 46.3544f);
        latcorners.set(3, 46.63433f);
        out.addGlobalAttribute("latcorners", latcorners);
        ArrayFloat.D1 loncorners = new ArrayFloat.D1(4);
        loncorners.set(0, -145.5f);
        loncorners.set(1, -68.32005f);
        loncorners.set(2, -2.569891f);
        loncorners.set(3, 148.6418f);
        out.addGlobalAttribute("loncorners", loncorners);
        out.addGlobalAttribute("platform", "Model");
        out.addGlobalAttribute("references", "");
        out.addGlobalAttribute("standardpar1", new Double(50.0));
        out.addGlobalAttribute("standardpar2", new Double(50.000001));
        out.addGlobalAttribute("stream", "s4");
        out.addGlobalAttribute("title", "Daily NARR");
        
        // Dimensions
        
        Dimension timeDim = out.addUnlimitedDimension("time");
        Dimension yDim = out.addDimension("y", 277);
        Dimension xDim = out.addDimension("x", 349);
        Dimension nbDim = out.addDimension("nbnds", 2);
        
        // Variables
        
        out.addVariable("time", DataType.DOUBLE, new Dimension[] { timeDim });
        out.addVariableAttribute("time", "avg_period", "0000-00-01 00:00:00");
        out.addVariableAttribute("time", "units", "hours since 1800-1-1 00:00:0.0");
        out.addVariableAttribute("time", "axis", "T");
        out.addVariableAttribute("time", "coordinate_defines", "start");
        out.addVariableAttribute("time", "delta_t", "0000-00-01 00:00:00");
        out.addVariableAttribute("time", "long_name", "analysis time");
        out.addVariableAttribute("time", "standard_name", "time");
        
        out.addVariable("lat", DataType.FLOAT, new Dimension[] { yDim, xDim });
        out.addVariableAttribute("lat", "axis", "Y");
        out.addVariableAttribute("lat", "coordinate_defines", "point");
        out.addVariableAttribute("lat", "long_name", "latitude coordinate");
        out.addVariableAttribute("lat", "standard_name", "latitude");
        out.addVariableAttribute("lat", "units", "degrees_north");
        
        out.addVariable("lon", DataType.FLOAT, new Dimension[] { yDim, xDim });
        out.addVariableAttribute("lon", "axis", "X");
        out.addVariableAttribute("lon", "coordinate_defines", "point");
        out.addVariableAttribute("lon", "long_name", "longitude coordinate");
        out.addVariableAttribute("lon", "standard_name", "longitude");
        out.addVariableAttribute("lon", "units", "degrees_east");
        
        out.addVariable("y", DataType.FLOAT, new Dimension[] { yDim });
        out.addVariableAttribute("y", "long_name", "northward distance from southwest corner of domain in projection coordinates");
        out.addVariableAttribute("y", "standard_name", "projection_y_coordinate");
        out.addVariableAttribute("y", "units", "m");

        out.addVariable("x", DataType.FLOAT, new Dimension[] { xDim });
        out.addVariableAttribute("x", "long_name", "eastward distance from southwest corner of domain in projection coordinates");
        out.addVariableAttribute("x", "standard_name", "projection_x_coordinate");
        out.addVariableAttribute("x", "units", "m");
        
        out.addVariable("Lambert_Conformal", DataType.INT, new Dimension[0]);
        out.addVariableAttribute("Lambert_Conformal", "false_easting", new Double(5632642.22547));
        out.addVariableAttribute("Lambert_Conformal", "false_northing", new Double(4612545.65137));
        out.addVariableAttribute("Lambert_Conformal", "grid_mapping_name", "lambert_conformal_conic");
        out.addVariableAttribute("Lambert_Conformal", "latitude_of_projection_origin", new Double(50.0));
        out.addVariableAttribute("Lambert_Conformal", "longitude_of_central_meridian", new Double(-107.0));
        ArrayDouble.D1 parallels = new ArrayDouble.D1(2);
        parallels.set(0, 50.0);
        parallels.set(1, 50.0);
        out.addVariableAttribute("Lambert_Conformal", "standard_parallel", parallels);

        out.addVariable("time_bnds", DataType.DOUBLE, new Dimension[] { nbDim });
        out.addVariableAttribute("time_bnds", "long_name", "Time Boundaries");
        
        out.addVariable("gdd", DataType.SHORT, new Dimension[] { timeDim, yDim, xDim });
        out.addVariableAttribute("gdd", "cell_methods", "time: mean (of each 3-hourly interval) mean (of 8 3-hourly means)");
        out.addVariableAttribute("gdd", "_FillValue", Short.valueOf((short)-99));
        out.addVariableAttribute("gdd", "missing_value", Short.valueOf((short)-99));
        out.addVariableAttribute("gdd", "coordinates", "lat lon");
        out.addVariableAttribute("gdd", "dataset", "NARR Daily Averages");
        out.addVariableAttribute("gdd", "grid_mapping", "Lambert_Conformal");
        out.addVariableAttribute("gdd", "level_desc", "2 m");
        out.addVariableAttribute("gdd", "long_name", "Growing Degree Days");
        out.addVariableAttribute("gdd", "parent_stat", "Individual Obs");
        out.addVariableAttribute("gdd", "statistic", "Mean");
        out.addVariableAttribute("gdd", "units", "degC*d");
        out.addVariableAttribute("gdd", "var_desc", "Growing degree days");
    }
    
    public static void main (String[] args) throws IOException, InvalidRangeException {
        GridDataset in = ucar.nc2.dt.grid.GridDataset.open(args[0]);
        NetcdfFileWriteable out = null;
        float threshold = 283.15f;
        try {
            GridDataset.Gridset grids = getTimeGrid(in);
            GridCoordSystem gcs = grids.getGeoCoordSystem();
            CoordinateAxis xAxis = gcs.getXHorizAxis();
            CoordinateAxis yAxis = gcs.getYHorizAxis();
            CoordinateAxis1DTime tAxis1D = gcs.getTimeAxis1D();
            Index index = new Index3D(new int[] { 
                    (int)tAxis1D.getSize(),
                    (int)yAxis.getSize(),
                    (int)xAxis.getSize()
                });
            
            out = NetcdfFileWriteable.createNew(args[1]);
            setupDataset(out);
            
            out.create();
            
            out.write("y", yAxis.read());
            out.write("x", xAxis.read());
            out.write("lat", ((Variable)in.getDataVariable("lat")).read());
            out.write("lon", ((Variable)in.getDataVariable("lon")).read());
            
            GridDatatype grid = grids.getGrids().get(0);
            ArrayShort.D3 prev = new ArrayShort.D3(1, (int)yAxis.getSize(), (int)xAxis.getSize());
            Arrays.fill((short[])prev.getStorage(), (short)0);
            ArrayShort.D3 current = new ArrayShort.D3(1, (int)yAxis.getSize(), (int)xAxis.getSize()); 
            Array time = Array.factory(DataType.DOUBLE, new int[] { 1 });
            Array times = tAxis1D.read();
            Array temp = grid.readDataSlice(-1, -1, -1, -1);
            for (int t = 0; t < tAxis1D.getSize(); t += 1) {
                index.setDim(0, t);
                time.setDouble(0, times.getDouble(t));
                out.write("time", new int[] { t }, time);
                for (int y = 0; y < yAxis.getSize(); y += 1) {
                    index.setDim(1, y);
                    for (int x = 0; x < xAxis.getSize(); x += 1) {
                        index.setDim(2, x);
                        float value = prev.get(0, y, x) + Math.max(0.0f, temp.getFloat(index) - threshold);
                        if (Float.isNaN(value))
                            value = -99f;
                        current.set(0, y, x, (short)Math.round(value));
                    }
                }
                out.write("gdd", new int[] { t, 0, 0 }, current);
                ArrayShort.D3 swap = current;
                prev = current;
                current = swap;
            }
            System.out.println("done");
        } finally {
            in.close();
            out.close();
        }
    }
}