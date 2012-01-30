// Copyright 2011 ESRI
// 
// All rights reserved under the copyright laws of the United States
// and applicable international laws, treaties, and conventions.
// 
// You may freely redistribute and use this sample code, with or
// without modification, provided you include the original copyright
// notice and use restrictions.
// 
// See use restrictions at /arcgis/developerkit/userestrictions.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.esriSystem;
using System.Windows.Forms;

namespace AGS_CommandLineSOM
{
  class Program
  {
    //Ctrl+G keyboard Bell for error messages
    const string ErrorBell = "\x7";
    const string Build = "v10.0.2";

    enum serviceState
    {
      Start,
      Stop,
      Restart,
      Pause
    }

    static void Main(string[] args)
    {

      try
      {
        //print usage if no arguments were passed
        if (args.Length < 1)
        {
          usage();
          return;
        }

        //read service name
        string arg0 = args[0];
        int iServerArgIncrement = 0;
        string sCommand = null;
        string sServer = null;
        if (arg0.StartsWith("-"))
          sCommand = args[0];
        else
        {
          iServerArgIncrement = 1;
          sServer = args[0];
          if (args.Length > 1)
            sCommand = args[1];
        }

        //print usage if asking for help
        if ((sCommand == null) || (!sCommand.StartsWith("-")))
        {
          Console.WriteLine(ErrorBell + "\nError: No operation specified!");
          usage();
          return;
        }
        else if (sCommand == "-h")
        {
          usage(true);
          return;
        }

        #region AGS 10 initialization

        if (!ESRI.ArcGIS.RuntimeManager.Bind(ESRI.ArcGIS.ProductCode.EngineOrDesktop))
        {
          //1/7/2011
          //Console.WriteLine("\nCould not find an available ArcGIS Engine or Desktop product to initialize.");
          if (!ESRI.ArcGIS.RuntimeManager.Bind(ESRI.ArcGIS.ProductCode.Server))
          {
            Console.WriteLine("\nCould not find an available ArcGIS Engine, Desktop or Server product to initialize.");
          }

        }

        // Usual engine initialization code follows from here (AoInitialize).
        IAoInitialize init = new AoInitializeClass();

        #endregion

        string sService = "";
        string sType = "";
        int iCount = 0;
        
        int iServiceArg = (1 + iServerArgIncrement);
        if (args.Length > iServiceArg)
        {
          sService = args[iServiceArg];
        }

        string sDataParam = "";
        //if (args.Length > 3)
        //{
        //  sDataParam = args[3];
        //}

        //read type 
        //string sType = "MapServer";
        int iTypeArg = (2 + iServerArgIncrement);
        //if (args.Length > iTypeArg)
        //{ sType = args[iTypeArg]; }

        //11.30.2010, check and see if param is type or optional param for stats, etc:
        //12.10,2010, revised to pickup optional <service> and <minutes> for stats
        if (args.Length > iTypeArg)
        {
          if (args[iTypeArg].IndexOf("Server") > 1)
            sType = args[iTypeArg++];
          else
            sDataParam = args[iTypeArg++];

          if (args.Length > iTypeArg)
            sDataParam = args[iTypeArg];
        }


        if (sServer == null) sServer = "localhost";
        //connect to the GIS Server
        GISServerConnection pGISServerConnection = null;
        try
        {
          pGISServerConnection = new GISServerConnectionClass();
          pGISServerConnection.Connect(sServer);
        }
        catch (Exception e)
        {
          Console.WriteLine(ErrorBell + "\nError: Could not connect to server.");
          Console.WriteLine("\nTips: Verify server name and availability of ArcGIS Server.");
          Console.WriteLine("      Verify that this user is a member of the AGSADMIN group on server.");
          Console.WriteLine("\nDetails: " + e.Message);
          Environment.Exit(1);
          return;
        }

        //read configurations
        IServerObjectAdmin2 pServerObjectAdmin = pGISServerConnection.ServerObjectAdmin as IServerObjectAdmin2;
        IEnumServerObjectConfiguration pConfigs = pServerObjectAdmin.GetConfigurations();
        IServerObjectConfiguration pConfig = pConfigs.Next();

        switch (sCommand)
        {
          case "-start"://start
          case "-s"://start
              if (sService.ToUpper() == "*ALL*" || sService.ToUpper() == "*ALL")
                StopStartAll(pServerObjectAdmin, serviceState.Start);
              else
              {
                Console.WriteLine();
                if (sService == "")
                {
                  Console.WriteLine(ErrorBell + "Input error: Missing required 'servicename'");
                  usage();
                  return;
                }
                else
                {
                  if (sType == "")
                    sType = "MapServer";

                  StartService(pServerObjectAdmin, sService, sType);
                }
              }
              break;

          case "-stop"://stop
          case "-x"://stop
              if (sService.ToUpper() == "*ALL*" || sService.ToUpper() == "*ALL")
                StopStartAll(pServerObjectAdmin, serviceState.Stop);
              else
              {
                Console.WriteLine();
                if (sService == "")
                {
                  Console.WriteLine(ErrorBell + "Input error: Missing required 'servicename'");
                  usage();
                  return;
                }
                else
                {
                  if (sType == "")
                    sType = "MapServer";

                  StopService(pServerObjectAdmin, sService, sType);
                }
              }

              break;

          case "-restart"://restart
          case "-r"://restart
              if (sService.ToUpper() == "*ALL*" || sService.ToUpper() == "*ALL")
                StopStartAll(pServerObjectAdmin, serviceState.Restart);
              else
              {
                Console.WriteLine();
                if (sService == "")
                {
                  Console.WriteLine(ErrorBell + "Input error: Missing required 'servicename'");
                  usage();
                  return;
                }
                else
                {
                  if (sType == "")
                    sType = "MapServer";

                  StopService(pServerObjectAdmin, sService, sType);
                  StartService(pServerObjectAdmin, sService, sType);
                }
              }
              break;

          case "-pause"://pause
          case "-p"://pause
              if (sService.ToUpper() == "*ALL*" || sService.ToUpper() == "*ALL")
                StopStartAll(pServerObjectAdmin, serviceState.Pause);
              else
              {
                Console.WriteLine();
                if (sService == "")
                {
                  Console.WriteLine(ErrorBell + "Input error: Missing required 'servicename'");
                  usage();
                  return;
                }
                else
                {
                  if (sType == "")
                    sType = "MapServer";

                  PauseService(pServerObjectAdmin, sService, sType);
                }
              }

              break;

          case "-delete"://delete
              Console.WriteLine();

              if (sService == "")
              {
                Console.WriteLine(ErrorBell + "Input error: Missing required 'servicename'");
                usage();
                return;
              }
              else
              {
                if (sType == "")
                {
                  Console.WriteLine(ErrorBell + "Input error: Missing or invalid 'servicetype'");
                  usage();
                  return;
                }
                else
                  DeleteService(pServerObjectAdmin, sService, sType, sDataParam != "N");
              }

              break;

          case "-list"://list configurations
              Console.WriteLine("\nService Status:\n");
              pConfigs = pServerObjectAdmin.GetConfigurations();
              pConfig = pConfigs.Next();
              iCount = 0;

              do
              {
                if ((sService == "") || (sService == pConfig.TypeName) || (pConfig.Name.ToUpper().Contains(sService.ToUpper())))
                {
                  if ((sType == "") || (pConfig.TypeName == sType))
                  {
                    string sName = pConfig.Name;
                    string sTypeName = pConfig.TypeName;
                    IServerObjectConfigurationStatus pStatus = pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName);

                    string sStatus = pStatus.Status.ToString().Substring(6);

                    Console.WriteLine(sTypeName + " '" + sName + "': " + sStatus);
                    iCount += 1;
                  }
                }
                pConfig = pConfigs.Next();
              } while (pConfig != null);

              if (iCount == 0)
                Console.WriteLine("No Service candidates found.");
              else
                Console.WriteLine(string.Format("\nServices found: {0}", iCount));

              break;

          case "-listtypes":
              IEnumServerObjectType pEnumType = pServerObjectAdmin.GetTypes();
              IEnumServerObjectExtensionType pEnumExType = null;
              IServerObjectExtensionType pExType = null;
              pEnumType.Reset();
              IServerObjectType pSoType = pEnumType.Next();
              Console.WriteLine("\nThis server supports the following service types:\n");
              while (pSoType != null)
              {
                Console.WriteLine(pSoType.Name);
                pEnumExType = pServerObjectAdmin.GetExtensionTypes(pSoType.Name);
                pEnumExType.Reset();
                pExType = pEnumExType.Next();
                while (pExType != null)
                {
                  Console.WriteLine(String.Format("     Extension: {0}", pExType.Name));
                  pExType = pEnumExType.Next();
                }
                pSoType = pEnumType.Next();
              }

              break;

          case "-publish":
              if (sService == "")
              {
                Console.WriteLine(ErrorBell + "\nInput error: Missing required 'servicename'");
                usage();
                return;
              }
              else
              {
                FileInfo fileInfo = new FileInfo(sService);

                if (fileInfo.Extension == "")
                {
                  if (sService.Substring(sService.Length-1) == ".")
                    fileInfo = new FileInfo(sService + "mxd");
                  else
                    fileInfo = new FileInfo(sService + ".mxd");
                }

                if (sType == "")
                  sType = "MapServer";

                if (sType.ToUpper() == "MAPSERVER")
                {
                  if (fileInfo.Exists)
                  {
                    if (sDataParam != "")
                      sService = sDataParam; //get user defined service name
                    else
                      sService = fileInfo.Name.Substring(0, fileInfo.Name.LastIndexOf(@".")).Replace(" ", "");

                    Console.WriteLine(string.Format("\nAttempting to publish: '{0}'", fileInfo.FullName));
                    CreateMapService(sService, fileInfo.FullName, pServerObjectAdmin);
                    Console.WriteLine(string.Format("Successfully published as {0} '{1}'", sType, sService));
                    //MessageBox.Show("Published " + sService + ".");
                  }
                  else
                  {
                    Console.WriteLine(ErrorBell + "\nError: Unable to locate Map Document file specified.");
                    return;
                  }
                }
                else
                {
                  Console.WriteLine(ErrorBell + "\nInput error: Unsupported 'servicetype' specified.");
                  usage();
                  return;
                }
              }

              break;

          case "-stats":
              esriServerTimePeriod eTimePeriod = esriServerTimePeriod.esriSTPNone;
              int iLength = 1;
              int iRange = 1;

              //true if Server Summary only!
              bool bSummaryOnly = true;
              statsData sStatistics = new statsData();

              #region is the servicename a number? then it's probably the starttime option.

              if ((sService != "") && (int.TryParse(sService, out iCount) || int.TryParse(sService.Substring(0, sService.Length - 1), out iCount)))
              {
                sDataParam = sService;
                sService = "";
              }

              #endregion

              #region prep starttime interval

              if (sDataParam != "")
              {
                if (int.TryParse(sDataParam, out iLength))
                {
                  //no period specified, default to minutes
                  iRange = 61;
                  eTimePeriod = esriServerTimePeriod.esriSTPMinute;
                }
                else
                {
                  if (int.TryParse(sDataParam.Substring(0, sDataParam.Length - 1), out iLength))
                  {
                    switch (sDataParam.Substring(sDataParam.Length - 1).ToLower())
                    {
                      case "s"://seconds
                        iRange = 61;
                        eTimePeriod = esriServerTimePeriod.esriSTPSecond;
                        break;
                      case "m"://minutes
                        iRange = 61;
                        eTimePeriod = esriServerTimePeriod.esriSTPMinute;

                        break;
                      case "h"://hours
                        iRange = 25;
                        eTimePeriod = esriServerTimePeriod.esriSTPHour;

                        break;
                      case "d"://days
                        iRange = 31;
                        eTimePeriod = esriServerTimePeriod.esriSTPDay;

                        break;
                      default:
                        Console.WriteLine(ErrorBell + "\nInput error: Unknown 'starttime' units specified.");
                        usage();
                        return;
                    }
                  }
                  else
                  {
                    Console.WriteLine(ErrorBell + "\nInput error: Invalid 'starttime' value specified.");
                    usage();
                    return;
                  }
                }

                //add one to interval and see if it is in acceptable range
                if ((++iLength < 1) || (iLength > iRange))
                {
                  Console.WriteLine(ErrorBell + "\nInput error: Invalid 'starttime' interval specified, outside acceptable range.");
                  usage();
                  return;
                }
              }

              #endregion

              if (sService.ToUpper() == "*ALL*" || sService.ToUpper() == "*ALL")
              {
                sService = "";
                sType = "";

                //report filtered Service details not just Server Summary
                bSummaryOnly = false;
              }

              if (sService != "" || sType != "")
              {
                //report filtered Service details not just Server Summary
                bSummaryOnly = false;
              }
            
              pConfigs = pServerObjectAdmin.GetConfigurations();
              pConfig = pConfigs.Next();
              iCount = 0;

              do
              {
                if ((sService == "") || (sService == pConfig.TypeName) || (pConfig.Name.ToUpper().Contains(sService.ToUpper())))
                {
                  if ((sType == "") || (pConfig.TypeName == sType))
                  {
                    //gather Instance details regardless of statistics reporting
                    gatherInstanceStatistics(pConfig.Name, pConfig.TypeName, sStatistics, pServerObjectAdmin);

                    if (!bSummaryOnly)
                    {
                      //gather Service statistics if not just Server Summary
                      gatherServerStatistics(pConfig.Name, pConfig.TypeName, iLength, sStatistics, eTimePeriod, pServerObjectAdmin);
                      printServerStatistics(pConfig.Name, pConfig.TypeName, sStatistics, false);
                    }
                  }
                }
                pConfig = pConfigs.Next();
              } while (pConfig != null);


              if (bSummaryOnly)
              {
                //gather Server Summary Statistics
                gatherServerStatistics("", "", iLength, sStatistics, eTimePeriod, pServerObjectAdmin);
              }

              //print Server or Service Summary
              printServerStatistics("", "", sStatistics, !bSummaryOnly);

              break;

          case "-describe"://describe status
              Console.WriteLine("\nService Description(s):");
              pConfigs = pServerObjectAdmin.GetConfigurations();
              pConfig = pConfigs.Next();
              iCount = 0;

              do
              {
                if ((sService == "") || (sService == pConfig.TypeName) || (pConfig.Name.ToUpper().Contains(sService.ToUpper())))
                {
                  if ((sType == "") || (pConfig.TypeName == sType))
                  {
                    describeService(pConfig.Name, pConfig.TypeName, pServerObjectAdmin);

                    iCount += 1;
                  }
                }
                pConfig = pConfigs.Next();
              } while (pConfig != null);

              if (iCount == 0)
                Console.WriteLine("\nNo Service candidates found.");
              else
                Console.WriteLine(string.Format("\nServices found: {0}", iCount));

              break;

          default:
              Console.WriteLine(ErrorBell + string.Format("\nInput error: Unknown operation '{0}'", sCommand));
              usage();
              return;
        }
      }
      catch (Exception ex)
      {

        Console.WriteLine(ErrorBell + "\nError: " + ex.Message);
        Environment.Exit(1);
        return;
      }

      return;
    }

    static void describeService(string sService, string sType, IServerObjectAdmin2 pServerObjectAdmin)
    {
      IServerObjectConfiguration2 pSOConfig = (IServerObjectConfiguration2)pServerObjectAdmin.GetConfiguration(sService, sType);

      IServerObjectConfigurationStatus pSOConfigStatus = (IServerObjectConfigurationStatus)pServerObjectAdmin.GetConfigurationStatus(sService, sType);

      string sStatus = pSOConfigStatus.Status.ToString().Substring(6);

      Console.WriteLine("\nService Name: '" + sService + "'");
      Console.WriteLine("   Type: " + sType);
      Console.WriteLine("   Status: " + sStatus);

      Console.WriteLine("   Description: " + pSOConfig.Description);
      Console.WriteLine("   Pooled?: " + pSOConfig.IsPooled);
      Console.WriteLine("   Isolation Level: " + GetIsolationLevel(pSOConfig.IsolationLevel));
      Console.WriteLine("   Max Instance: " + pSOConfig.MaxInstances);
      Console.WriteLine("   Min Instance: " + pSOConfig.MinInstances);

      Console.WriteLine("   Instances Running: " + pSOConfigStatus.InstanceCount);
      Console.WriteLine("   Instances In Use: " + pSOConfigStatus.InstanceInUseCount);

      Console.WriteLine("   Startup: " + GetStartupType(pSOConfig.StartupType));
      Console.WriteLine("   Usage timeout (seconds): " + pSOConfig.UsageTimeout);
      Console.WriteLine("   Wait timeout (seconds): " + pSOConfig.WaitTimeout);
      Console.WriteLine("   Cleanup timeout (seconds): " + pSOConfig.CleanupTimeout);
      Console.WriteLine("   Startup timeout (seconds): " + pSOConfig.CleanupTimeout);

      writeOutPropertySet(pSOConfig.Properties, "Properties", "   ");
      writeOutPropertySet(pSOConfig.RecycleProperties, "Recycling properties", "   ");
      writeOutPropertySet(pSOConfig.Info, "Additional info", "   ");

      IEnumServerObjectExtensionType pEnumExType = pServerObjectAdmin.GetExtensionTypes(pSOConfig.TypeName);
      pEnumExType.Reset();
      IServerObjectExtensionType pExType = pEnumExType.Next();
      int iEnabledExtensions = 0;
      while (pExType != null)
      {
        if (pSOConfig.get_ExtensionEnabled(pExType.Name))
        {
          if (iEnabledExtensions == 0)
          {
            Console.WriteLine();
            Console.WriteLine("***** Additional capabilities *****");
            Console.WriteLine();
          }
          iEnabledExtensions++;
          Console.WriteLine(String.Format("   {0}", pExType.Name));
          writeOutPropertySet(pSOConfig.get_ExtensionInfo(pExType.Name), "Extension info", "      ");
          writeOutPropertySet(pSOConfig.get_ExtensionProperties(pExType.Name), "Extension properties", "      ");
        }
        pExType = pEnumExType.Next();
      }
    }

    /// <summary>Custom Statistics class for Service Data collection and accumulation (summary totals)</summary>
    public class statsData
    {
      /// <summary>Contains Service Usage Time details</summary>
      public stat UsageTime = new stat();
      /// <summary>Contains Service Wait Time details</summary>
      public stat WaitTime = new stat();
      /// <summary>Contains Service Creation Time details</summary>
      public stat CreationTime = new stat();
      /// <summary>Contains Service Instance details</summary>
      public stat Instances = new stat();

      private static bool bAccummulative = false;

      /// <summary>Status of Service (Current only)</summary>
      public string Status = "";

      /// <summary>Start time of statistical data</summary>
      public DateTime StartTime;
      /// <summary>End time of statistical data</summary>
      public DateTime EndTime;

      /// <summary>Set to True if you wish to return Accummulated details rather than current details</summary>
      public bool ReturnAccummulative
      {
        get
        {
          return bAccummulative;
        }

        set
        {
          bAccummulative = value;
        }
      }

      public class stat
      {
        private int iSuccess = 0;
        private int iAccumSuccess = 0;
        private int iFailure = 0;
        private int iAccumFailure = 0;
        private int iTimeout = 0;
        private int iAccumTimeout = 0;
        private double dMin = -1;
        private double dAccumMin = -1;
        private double dMax = 0;
        private double dAccumMax = 0;
        private double dSum = 0;
        private double dAccumSum = 0;

        /// <summary>Get or Set number of Successful requests. Value is automatically added to Accummulation.</summary>
        public int Success
        {
          get 
          {
            if (bAccummulative)
              return iAccumSuccess;
            else
              return iSuccess;
          }

          set
          {
            iSuccess = value;
            if (!bAccummulative) iAccumSuccess += value;
          }
        }

        /// <summary>Get or Set number of Failed requests. Value is automatically added to Accummulation.</summary>
        public int Failure
        {
          get
          {
            if (bAccummulative)
              return iAccumFailure;
            else
              return iFailure;
          }

          set
          {
            iFailure = value;
            if (!bAccummulative) iAccumFailure += value;
          }
        }

        /// <summary>Get or Set number of Timed out requests. Value is automatically added to Accummulation.</summary>
        public int Timeout
        {
          get
          {
            if (bAccummulative)
              return iAccumTimeout;
            else
              return iTimeout;
          }

          set
          {
            iTimeout = value;
            if (!bAccummulative) iAccumTimeout += value;
          }
        }

        /// <summary>Get total number of requests (Success+Failure+Timeout)</summary>
        public int Count
        {
          get
          {
            if (bAccummulative)
              return iAccumSuccess + iAccumFailure + iAccumTimeout;
            else
              return iSuccess + iFailure + iTimeout;
          }
        }

        /// <summary>Get or Set Minimum value (could be time in seconds or other). Value is automatically added to Accummulation.</summary>
        public double Minimum
        {
          get
          {
            if (bAccummulative)
            {
              if (dAccumMin < 0)
                return 0;
              else
                return dAccumMin;
            }
            else
            {
              if (dMin < 0)
                return 0;
              else
                return dMin;
            }
          }

          set
          {
            dMin = value;

            if (!bAccummulative)
            {
              if (dAccumMin < 0)
                dAccumMin = value;
              else if (value < dAccumMin)
                dAccumMin = value;
            }
          }
        }

        /// <summary>Get or Set Maximum value (could be time in seconds or other). Value is automatically added to Accummulation.</summary>
        public double Maximum
        {
          get
          {
            if (bAccummulative)
              return dAccumMax;
            else
              return dMax;
          }

          set
          {
            dMax = value;

            if (!bAccummulative && (value > dAccumMax)) dAccumMax = value;
          }
        }

        /// <summary>Get or Set Summary value (could be total time in seconds or other). Value is automatically added to Accummulation.</summary>
        public double Sum
        {
          get
          {
            if (bAccummulative)
              return dAccumSum;
            else
              return dSum;
          }

          set
          {
            dSum = value;
            if (!bAccummulative) dAccumSum += value;
          }
        }

        /// <summary>Get Average value (Sum/Success)</summary>
        public double Average
        {
          get
          {
            if (Success > 0)
              return Sum / Success;
            else
              return 0;
          }
        }
      }
    }

    /// <summary>
    /// gather Service and Instance Statistics
    /// </summary>
    /// <param name="sService">Name of Service</param>
    /// <param name="sType">Service Type</param>
    /// <param name="cStats">Current Statistics Object</param>
    /// <param name="pServerObjectAdmin">Administrative Object</param>
    static void gatherInstanceStatistics(string sService, string sType, statsData cStats, IServerObjectAdmin2 pServerObjectAdmin)
    {
      IServerObjectConfigurationStatus pStatus = (IServerObjectConfigurationStatus)pServerObjectAdmin.GetConfigurationStatus(sService, sType);
      cStats.Status = pStatus.Status.ToString().Substring(6);

      IServerObjectConfiguration2 pConfig = (IServerObjectConfiguration2)pServerObjectAdmin.GetConfiguration(sService, sType);

      cStats.Instances.Success = 1;                     //Set Service count
      cStats.Instances.Minimum = pConfig.MinInstances;  //Current Minimum limit
      cStats.Instances.Maximum = pConfig.MaxInstances;  //Current Maximum limit
      cStats.Instances.Sum = pStatus.InstanceCount;     //Current running instances
    }

    /// <summary>
    /// gather Service and Server Statistics
    /// </summary>
    /// <param name="sService">Name of Service or blank for Summary(Server or Service)</param>
    /// <param name="sType">Service Type or blank for Summary(Server or Service)</param>
    /// <param name="iLength">Number of Time Periods to collect</param>
    /// <param name="cStats">Current Statistics Object</param>
    /// <param name="eTimePeriod">Time Period type(seconds, minutes, hours, or days)</param>
    /// <param name="pServerObjectAdmin">Administrative Object</param>
    static void gatherServerStatistics(string sService, string sType, int iLength, statsData cStats, esriServerTimePeriod eTimePeriod, IServerObjectAdmin2 pServerObjectAdmin)
    {

      IServerStatistics pServerStats = (IServerStatistics)pServerObjectAdmin;
      IServerTimeRange pStr;
      IStatisticsResults pStats;

      int iIndex = 0;

      #region Service Usage Time

      pStats = pServerStats.GetAllStatisticsForTimeInterval(esriServerStatEvent.esriSSEContextReleased, eTimePeriod, iIndex, iLength, sService, sType, "");
      pStr = (IServerTimeRange)pStats;

      cStats.UsageTime.Success = pStats.Count;
      cStats.UsageTime.Minimum = pStats.Minimum;
      cStats.UsageTime.Maximum = pStats.Maximum;
      cStats.UsageTime.Sum = pStats.Sum;

      pStats = pServerStats.GetAllStatisticsForTimeInterval(esriServerStatEvent.esriSSEContextUsageTimeout, eTimePeriod, iIndex, iLength, sService, sType, "");
      cStats.UsageTime.Timeout = pStats.Count;

      #endregion

      #region Service Wait Time

      pStats = pServerStats.GetAllStatisticsForTimeInterval(esriServerStatEvent.esriSSEContextCreated, eTimePeriod, iIndex, iLength, sService, sType, "");

      cStats.WaitTime.Success = pStats.Count;
      cStats.WaitTime.Minimum = pStats.Minimum;
      cStats.WaitTime.Maximum = pStats.Maximum;
      cStats.WaitTime.Sum = pStats.Sum;

      pStats = pServerStats.GetAllStatisticsForTimeInterval(esriServerStatEvent.esriSSEContextCreationTimeout, eTimePeriod, iIndex, iLength, sService, sType, "");
      cStats.WaitTime.Timeout = pStats.Count;

      pStats = pServerStats.GetAllStatisticsForTimeInterval(esriServerStatEvent.esriSSEContextCreationFailed, eTimePeriod, iIndex, iLength, sService, sType, "");
      cStats.WaitTime.Failure = pStats.Count;

      #endregion

      #region Service Creation Time

      pStats = pServerStats.GetAllStatisticsForTimeInterval(esriServerStatEvent.esriSSEServerObjectCreated, eTimePeriod, iIndex, iLength, sService, sType, "");
      cStats.CreationTime.Success = pStats.Count;
      cStats.CreationTime.Minimum = pStats.Minimum;
      cStats.CreationTime.Maximum = pStats.Maximum;
      cStats.CreationTime.Sum = pStats.Sum;

      pStats = pServerStats.GetAllStatisticsForTimeInterval(esriServerStatEvent.esriSSEServerObjectCreationFailed, eTimePeriod, iIndex, iLength, sService, sType, "");
      cStats.CreationTime.Failure = pStats.Count;

      #endregion

      if (pStr != null)
      {
        cStats.StartTime = pStr.StartTime;
        cStats.EndTime = pStr.EndTime;
      }
    }

    //global variable used to control printing of Time Range when multiple services are being listed
    static Boolean statsFlag = true;

    /// <summary>
    /// Print Service statistics details
    /// </summary>
    /// <param name="sService">Name of Service or blank for Summary(Server or Service)</param>
    /// <param name="sType">Service Type or blank for Summary(Server or Service)</param>
    /// <param name="cStats">Current Statistics Object</param>
    /// <param name="Summary">True if printing summary of services. False for Server summary and Service specific reporting.</param>
    static void printServerStatistics(string sService, string sType, statsData cStats, bool Summary)
    {

      if (statsFlag && (cStats.Instances.Count > 0))
      {
        Console.WriteLine("\nStatistics Time Range: ");
        Console.WriteLine("  Start Time: " + cStats.StartTime.ToString());
        Console.WriteLine("  End Time: " + cStats.EndTime.ToString());
        statsFlag = false;
      }

      if (sService == "")
      {
        // turn on Accummulative stats reporting for Service and Server Summaries
        cStats.ReturnAccummulative = true;

        if (Summary)
        {
          if (cStats.Instances.Count == 0)
          {
            Console.WriteLine("\nNo Service candidates found.");
            return;
          }
          else
          {
            if (cStats.Instances.Count == 1)
            {
              Console.WriteLine("\nTotal number of Service candidates: 1, No need for Summary...");
              return;
            }

            Console.WriteLine(string.Format("\nTotal number of Service candidates: {0}", cStats.Instances.Count));
          }

          Console.WriteLine("\nSummary:");
        }
        else
        {
          Console.WriteLine(string.Format("\nTotal number of Services: {0}", cStats.Instances.Count));

          Console.WriteLine("\nServer Summary:");
        }
      }
      else
      {
        // turn off Accummulative stats reporting for specific services
        cStats.ReturnAccummulative = false;

        Console.WriteLine("\nService Details:");
        Console.WriteLine("  Service Name: " + sService);
        Console.WriteLine("  Service Type: " + sType);
        Console.WriteLine("  Service Status: " + cStats.Status);
      }

      Console.WriteLine("\n  Service Instance Details:");
      Console.WriteLine("    Current Maximum: " + cStats.Instances.Maximum);
      Console.WriteLine("    Current Minimum: " + cStats.Instances.Minimum);
      Console.WriteLine("    Current Running: " + cStats.Instances.Sum);

      // set Accummulative stats reporting for Service Summary ONLY!
      cStats.ReturnAccummulative = Summary;

      TimeSpan reqTime = new TimeSpan(cStats.EndTime.Ticks - cStats.StartTime.Ticks);
      double nAvgReqPerSec = 0;

      if (reqTime.TotalSeconds > 0)
        nAvgReqPerSec = cStats.UsageTime.Count / reqTime.TotalSeconds;
      
      Console.WriteLine("\n  Service Usage Time:");
      Console.WriteLine("    Total number of requests: " + cStats.UsageTime.Count);
      Console.WriteLine("    Number of requests succeeded: " + cStats.UsageTime.Success);
      Console.WriteLine("    Number of requests timed out: " + cStats.UsageTime.Timeout);
      Console.WriteLine(string.Format("    Avg reqs / sec: {0:0.000000}", Math.Round(nAvgReqPerSec, 6)));
      Console.WriteLine(string.Format("    Avg usage time: {0:0.000000} Seconds", Math.Round(cStats.UsageTime.Average, 6)));
      Console.WriteLine(string.Format("    Min usage time: {0:0.000000} Seconds", Math.Round(cStats.UsageTime.Minimum, 6)));
      Console.WriteLine(string.Format("    Max usage time: {0:0.000000} Seconds", Math.Round(cStats.UsageTime.Maximum, 6)));
      Console.WriteLine(string.Format("    Sum usage time: {0:0.000000} Seconds", Math.Round(cStats.UsageTime.Sum, 6)));

      Console.WriteLine("\n  Service Wait Time:");
      Console.WriteLine("    Total number of requests: " + cStats.WaitTime.Count);
      Console.WriteLine("    Number of requests succeeded: " + cStats.WaitTime.Success);
      Console.WriteLine("    Number of requests failed: " + cStats.WaitTime.Failure);
      Console.WriteLine("    Number of requests timed out: " + cStats.WaitTime.Timeout);
      Console.WriteLine(string.Format("    Avg wait time: {0:0.000000} Seconds", Math.Round(cStats.WaitTime.Average, 6)));
      Console.WriteLine(string.Format("    Min wait time: {0:0.000000} Seconds", Math.Round(cStats.WaitTime.Minimum, 6)));
      Console.WriteLine(string.Format("    Max wait time: {0:0.000000} Seconds", Math.Round(cStats.WaitTime.Maximum, 6)));
      Console.WriteLine(string.Format("    Sum wait time: {0:0.000000} Seconds", Math.Round(cStats.WaitTime.Sum, 6)));

      Console.WriteLine("\n  Service Creation Time:");
      Console.WriteLine("    Total number of requests: " + cStats.CreationTime.Count);
      Console.WriteLine("    Number of requests succeeded: " + cStats.CreationTime.Success);
      Console.WriteLine("    Number of requests failed: " + cStats.CreationTime.Failure);
      Console.WriteLine(string.Format("    Avg creation time: {0:0.000000} Seconds", Math.Round(cStats.CreationTime.Average, 6)));
      Console.WriteLine(string.Format("    Min creation time: {0:0.000000} Seconds", Math.Round(cStats.CreationTime.Minimum, 6)));
      Console.WriteLine(string.Format("    Max creation time: {0:0.000000} Seconds", Math.Round(cStats.CreationTime.Maximum, 6)));
      Console.WriteLine(string.Format("    Sum creation time: {0:0.000000} Seconds", Math.Round(cStats.CreationTime.Sum, 6)));
    }

    static void StopStartAll(IServerObjectAdmin2 pServerObjectAdmin, serviceState state)
    {
      switch (state)
      {
        case serviceState.Start:
          Console.WriteLine("\nAttempting to start *all* stopped or paused services:\n");
          break;
        case serviceState.Stop:
          Console.WriteLine("\nAttempting to stop *all* running or paused services:\n");
          break;
        case serviceState.Restart:
          Console.WriteLine("\nAttempting to restart *all* running or paused services:");
          break;
        case serviceState.Pause:
          Console.WriteLine("\nAttempting to pause *all* running services:\n");
          break;
        default:
          break;
      }

      int iCount = 0;

      IEnumServerObjectConfiguration pConfigs = pServerObjectAdmin.GetConfigurations();
      IServerObjectConfiguration pConfig = pConfigs.Next();
      while (pConfig != null)
      {
        string sName = pConfig.Name;
        string sTypeName = pConfig.TypeName;
        IServerObjectConfigurationStatus pStatus = pServerObjectAdmin.GetConfigurationStatus(pConfig.Name, pConfig.TypeName); 
        switch (state)
        {
          case serviceState.Start:
            if (pStatus.Status == esriConfigurationStatus.esriCSStopped || pStatus.Status == esriConfigurationStatus.esriCSPaused)
            {
              StartService(pServerObjectAdmin, sName, sTypeName);
              iCount++; 
            }
            break;
          case serviceState.Stop:
            if (pStatus.Status == esriConfigurationStatus.esriCSStarted || pStatus.Status == esriConfigurationStatus.esriCSPaused)
            {
              StopService(pServerObjectAdmin, sName, sTypeName);
              iCount++;
            }
            break;
          case serviceState.Restart:
            if (pStatus.Status == esriConfigurationStatus.esriCSStarted || pStatus.Status == esriConfigurationStatus.esriCSPaused)
            {
              Console.WriteLine();
              StopService(pServerObjectAdmin, sName, sTypeName);
              StartService(pServerObjectAdmin, sName, sTypeName);
              iCount++;
            }
            break;
          case serviceState.Pause:
            if (pStatus.Status == esriConfigurationStatus.esriCSStarted)
            {
              PauseService(pServerObjectAdmin, sName, sTypeName);
              iCount++;
            }
            break;
          default:
            break;
        }
        pConfig = pConfigs.Next();
      }

      if (iCount == 0)
        Console.WriteLine("\nNo service candidates found.");
      else
        Console.WriteLine(string.Format("\nServices affected: {0}", iCount));
    }

    static void StartService(IServerObjectAdmin2 pServerObjectAdmin, string sName, string sTypeName)
    {
      try
      {
        Console.Write(string.Format("Attempting to start {0} '{1}': ", sTypeName, sName));

        IServerObjectConfigurationStatus pStatus = pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName);
        if (pStatus.Status == esriConfigurationStatus.esriCSStopped || pStatus.Status == esriConfigurationStatus.esriCSPaused)
        {
          pServerObjectAdmin.StartConfiguration(sName, sTypeName);

          if (pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName).Status == esriConfigurationStatus.esriCSStarted)
          {
            Console.WriteLine("Successfully started...");
          }
          else
          {
            Console.WriteLine("Could not be started.");
            Environment.Exit(1);
          }
        }
        else
        {
          switch (pStatus.Status)
          {
            case esriConfigurationStatus.esriCSDeleted:
              Console.WriteLine(string.Format("Can't be started because it was previously deleted.", sTypeName, sName));
              Environment.Exit(1);
              break;
            case esriConfigurationStatus.esriCSStarted:
              Console.WriteLine(string.Format("Is already started.", sTypeName, sName));
              Environment.Exit(0);
              break;
            case esriConfigurationStatus.esriCSStarting:
              Console.WriteLine(string.Format("Can't be started because it is already starting.", sTypeName, sName));
              Environment.Exit(1);
              break;
            case esriConfigurationStatus.esriCSStopping:
              Console.WriteLine(string.Format("Can't be started because it is currently stopping.", sTypeName, sName));
              Environment.Exit(1);
              break;
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(ErrorBell + "Error starting service.\n");
        Console.WriteLine(e.Message);
        Environment.Exit(1);
      }
    }

    static void StopService(IServerObjectAdmin2 pServerObjectAdmin, string sName, string sTypeName)
    {
      try
      {
        Console.Write(string.Format("Attempting to stop {0} '{1}': ", sTypeName, sName));

        IServerObjectConfigurationStatus pStatus = pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName);
        if (pStatus.Status == esriConfigurationStatus.esriCSStarted || pStatus.Status == esriConfigurationStatus.esriCSPaused)
        {
          pServerObjectAdmin.StopConfiguration(sName, sTypeName);

          if (pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName).Status == esriConfigurationStatus.esriCSStopped)
          {
            Console.WriteLine("Successfully stopped...");
          }
          else
          {
            Console.WriteLine("Could not be stopped.");
            Environment.Exit(1);
          }
        }
        else
        {
          switch (pStatus.Status)
          {
            case esriConfigurationStatus.esriCSDeleted:
              Console.WriteLine(string.Format("Can't be stopped because it was previously deleted.", sTypeName, sName));
              Environment.Exit(1);
              break;
            case esriConfigurationStatus.esriCSStopped:
              Console.WriteLine(string.Format("Is already stopped.", sTypeName, sName));
       
               // Environment.Exit(0);
         

              break;
            case esriConfigurationStatus.esriCSStarting:
              Console.WriteLine(string.Format("Can't be stopped because it is currently starting.", sTypeName, sName));
              Environment.Exit(1);
              break;
            case esriConfigurationStatus.esriCSStopping:
              Console.WriteLine(string.Format("Can't be stopped because it is already stopping.", sTypeName, sName));
              Environment.Exit(1);
              break;
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(ErrorBell + "Error stopping service.\n");
        Console.WriteLine(e.Message);
        Environment.Exit(1);
      }
    }

    static void DeleteService(IServerObjectAdmin2 pServerObjectAdmin, string sName, string sTypeName, bool confirm)
    {
      try
      {
        while (confirm)
        {
          Console.Write(string.Format("Delete '{0}' {1} service, are you sure (yes or no)? ", sName, sTypeName));
          switch (Console.ReadLine().ToLower())
          {
            case "yes":
              confirm = false;
              Console.WriteLine();
              break;
            case "no":
              Console.WriteLine("\nService deletion CANCELLED!");
              return;
          }
        }

        Console.Write(string.Format("Attempting to delete {0} '{1}': ", sTypeName, sName));

        string sPrefix = "Successfully ";

        IServerObjectConfigurationStatus pStatus = pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName);
        if (pStatus.Status == esriConfigurationStatus.esriCSStarted || pStatus.Status == esriConfigurationStatus.esriCSPaused)
        {
          pServerObjectAdmin.StopConfiguration(sName, sTypeName);

          if (pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName).Status == esriConfigurationStatus.esriCSStopped)
          {
            Console.Write(sPrefix + "stopped, ");
            sPrefix = "and ";
          }
          else
          {
            Console.WriteLine("Could not be stopped!");
            Environment.Exit(1);
          }
        }

        if (pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName).Status == esriConfigurationStatus.esriCSStopped)
        {
          pServerObjectAdmin.DeleteConfiguration(sName, sTypeName);

          try
          {
            if (pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName).Status == esriConfigurationStatus.esriCSDeleted)
            {
              //see exception for successful Exception when checking for deleted service
              Console.WriteLine(sPrefix + "deleted...");
            }
            else
            {
              Console.WriteLine("Could not be deleted!");
              Environment.Exit(1);
            }
          }

          catch (Exception e)
          {
            if (e.Message.IndexOf("not found") > 0)
              //deletion succeeded, service is not found!
              Console.WriteLine(sPrefix + "deleted...");
            else
              throw e;
          }
        }
        else
        {
          switch (pStatus.Status)
          {
            case esriConfigurationStatus.esriCSDeleted:
              Console.WriteLine(string.Format("Can't be deleted because it was previously deleted!", sTypeName, sName));
              Environment.Exit(0);
              break;
            case esriConfigurationStatus.esriCSStarting:
              Console.WriteLine(string.Format("Can't be deleted because it is currently starting!", sTypeName, sName));
              Environment.Exit(1);
              break;
            case esriConfigurationStatus.esriCSStopping:
              Console.WriteLine(string.Format("Can't be deleted because it is currently stopping!", sTypeName, sName));
              Environment.Exit(1);
              break;
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(ErrorBell + "Error deleting service!\n");
        Console.WriteLine(e.Message);
        Environment.Exit(1);
      }
    }

    static void PauseService(IServerObjectAdmin2 pServerObjectAdmin, string sName, string sTypeName)
    {
      try
      {
        Console.Write(string.Format("Attempting to pause {0} '{1}': ", sTypeName, sName));

        IServerObjectConfigurationStatus pStatus = pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName);
        if (pStatus.Status == esriConfigurationStatus.esriCSStarted)
        {
          pServerObjectAdmin.PauseConfiguration(sName, sTypeName);

          if (pServerObjectAdmin.GetConfigurationStatus(sName, sTypeName).Status == esriConfigurationStatus.esriCSPaused)
          {
            Console.WriteLine("Successfully paused...");
          }
          else
          {
            Console.WriteLine("Could not be paused!");
            Environment.Exit(1);
          }
        }
        else
        {
          switch (pStatus.Status)
          {
            case esriConfigurationStatus.esriCSDeleted:
              Console.WriteLine(string.Format("Can't be paused because it was previously deleted!", sTypeName, sName));
              Environment.Exit(1);
              break;
            case esriConfigurationStatus.esriCSStopped:
              Console.WriteLine(string.Format("Can't be paused because it is currently stopped!", sTypeName, sName));
              Environment.Exit(1);
              break;
            case esriConfigurationStatus.esriCSPaused:
              Console.WriteLine(string.Format("Is already paused!", sTypeName, sName));
              Environment.Exit(0);
              break;
            case esriConfigurationStatus.esriCSStarting:
              Console.WriteLine(string.Format("Can't be paused because it is currently starting!", sTypeName, sName));
              Environment.Exit(1);
              break;
            case esriConfigurationStatus.esriCSStopping:
              Console.WriteLine(string.Format("Can't be paused because it is currently stopping!", sTypeName, sName));
              Environment.Exit(1);
              break;
          }
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(ErrorBell + "Error pausing service!\n");
        Console.WriteLine(e.Message);
        Environment.Exit(1);
      }
    }

    static void writeOutPropertySet(ESRI.ArcGIS.esriSystem.IPropertySet pPropSet, string sTitle, string indent)
    {
        object oPropName = null;
        object oPropValue = null;
        pPropSet.GetAllProperties(out oPropName, out oPropValue);
        object[] oPropNameArray = oPropName as object[];
        object[] oPropValArray = oPropValue as object[];
        Console.WriteLine(string.Format("{0}{1}:",indent,sTitle));
        for (int i = 0; i < pPropSet.Count; i++)
        {
            Console.WriteLine(string.Format("{0}   {1}: {2}", indent, oPropNameArray[i], oPropValArray[i]));
        }
    }

    static void CreateMapService(string sServiceName, string sMXD, IServerObjectAdmin pServerObjectAdmin)
    {

      try
      {

        IServerObjectConfiguration3 pConfiguration = (IServerObjectConfiguration3)pServerObjectAdmin.CreateConfiguration() ;

        pConfiguration.Name = sServiceName;
        pConfiguration.TypeName = "MapServer";

        ESRI.ArcGIS.esriSystem.IPropertySet pProps = pConfiguration.Properties;
        pProps.SetProperty("FilePath", sMXD);

        pConfiguration.IsPooled = true;
        pConfiguration.MinInstances = 1;
        pConfiguration.MaxInstances = 3;

        //5/13/2010
        pConfiguration.RecycleProperties.SetProperty("Start", "00:00");
        pConfiguration.RecycleProperties.SetProperty("Interval", "86400");

        pConfiguration.Info.SetProperty("WebEnabled", "true");
        pConfiguration.Info.SetProperty("WebCapabilities", "Map,Query,Data");

        //get first server directory
        IEnumServerDirectory pESD = pServerObjectAdmin.GetServerDirectories();
        IServerDirectory2 pSD = (IServerDirectory2)pESD.Next();
        do
        {

          if (pSD.Type == esriServerDirectoryType.esriSDTypeOutput)
          {
            pProps.SetProperty("OutputDir", pSD.Path);
            pProps.SetProperty("VirtualOutputDir", pSD.URL);
            break;
          }

          pSD = (IServerDirectory2)pESD.Next();
        }while(pESD!=null);


        pProps.SetProperty("SupportedImageReturnTypes", "URL");
        pProps.SetProperty("MaxRecordCount", "500");
        pProps.SetProperty("MaxImageWidth", "2048");
        pProps.SetProperty("MaxImageHeight", "2048");
        pProps.SetProperty("IsCached", "false");
        pProps.SetProperty("CacheOnDemand", "false");
        pProps.SetProperty("IgnoreCache", "false");
        pProps.SetProperty("ClientCachingAllowed", "true");

        /////////

        pConfiguration.WaitTimeout = 10;
        pConfiguration.UsageTimeout = 120;

        pServerObjectAdmin.AddConfiguration(pConfiguration);

        pServerObjectAdmin.StartConfiguration(pConfiguration.Name, pConfiguration.TypeName);
      }
      catch(Exception ex)
      {

        System.Diagnostics.Debug.WriteLine(ErrorBell + ex.Message);
        Environment.Exit(1);
      }
    }

    static string GetStartupType(esriStartupType startupType)
    {
      return startupType.ToString().Substring(6);
    }

    static string GetIsolationLevel(esriServerIsolationLevel isolationLevel)
    {
      return isolationLevel.ToString().Substring(19);
    }

    static void usage()
    {
      usage(false);
    }
    
    static void usage( bool help)
    {
      Console.WriteLine("\nAGSSOM " + Build + ", usage:\n");
      Console.WriteLine("AGSSOM -h\n");
      Console.WriteLine("AGSSOM [server] {-s | -start}   {[servicename [servicetype]] | *all*}\n");
      Console.WriteLine("AGSSOM [server] {-x | -stop}    {[servicename [servicetype]] | *all*}\n");
      Console.WriteLine("AGSSOM [server] {-r | -restart} {[servicename [servicetype]] | *all*}\n");
      Console.WriteLine("AGSSOM [server] {-p | -pause}   {[servicename [servicetype]] | *all*}\n");
      Console.WriteLine("AGSSOM [server] -delete servicename servicetype [N]\n");
      Console.WriteLine("AGSSOM [server] -list [likename] [servicetype]\n");
      Console.WriteLine("AGSSOM [server] -listtypes\n");
      Console.WriteLine("AGSSOM [server] -describe [likename] [servicetype]\n");
      Console.WriteLine("AGSSOM [server] -publish MXDpath [servicetype] [servicename]\n");
      Console.WriteLine("AGSSOM [server] -stats [[[likename] [servicetype]] | *all*] [starttime]");

      if (help)
      {
        Console.WriteLine("\nOperations:");
        Console.WriteLine("         -h          extended help");
        Console.WriteLine("         -s          start a stopped or paused service");
        Console.WriteLine("         -x          stop a started or paused service");
        Console.WriteLine("         -r          restart (stop then start) a started or paused service");
        Console.WriteLine("         -p          pause a started service");
        Console.WriteLine("         -delete     delete or remove a service. First, stop it if running");
        Console.WriteLine("         -describe   describe service details. Default: all services.");
        Console.WriteLine("                     If 'servicetype' omitted, all types will be included.");
        Console.WriteLine("         -list       list status of services. Default: all services.");
        Console.WriteLine("                     If 'servicetype' omitted, all types will be included.");
        Console.WriteLine("         -listtypes  list supported service types");
        Console.WriteLine("         -publish    publish a service. If 'servicename' omitted, file");
        Console.WriteLine("                     name is used as service name. Currently, only");
        Console.WriteLine("                     supports MapServer 'servicetype'.");
        Console.WriteLine("         -stats      print service usage statistics. Default: server summary.");
        Console.WriteLine("                     If 'servicetype' omitted, all types will be incuded.");
        Console.WriteLine("                     If 'starttime' omitted, server start time will be used.");
        Console.WriteLine("\nOptions:");
        Console.WriteLine("         server      local or remote server name. Default: localhost");
        Console.WriteLine("         servicename case sensitive service name");
        Console.WriteLine("         likename    services containing like text in service name");
        Console.WriteLine("         *all*       all services are affected. Trailing asterisk is optional");
        Console.WriteLine("         servicetype case sensitive service type: MapServer(default),");
        Console.WriteLine("                     GeocodeServer, FeatureServer, GeometryServer,");
        Console.WriteLine("                     GlobeServer, GPServer, ImageServer, GeoDataServer");
        Console.WriteLine("         N           do not ask for confirmation");
        Console.WriteLine("         MXDpath     path and filename of Map Document to publish as a service.");
        Console.WriteLine("                     If file extension is omitted, MXD is used.");
        Console.WriteLine("         starttime   as '99' or '99x'. Past time interval to start mining");
        Console.WriteLine("                     for statistics. Optional 'x' indicates interval units:");
        Console.WriteLine("                     'S or s' for Seconds, with range from 0 to 60,");
        Console.WriteLine("                     'M or m' for Minutes(default), with range from 0 to 60,");
        Console.WriteLine("                     'H or h' for Hours, with range from 0 to 24, or");
        Console.WriteLine("                     'D or d' for Days, with range from 0 to 30");
        Console.WriteLine("                     Tip: 0 is the current interval");
      }
        
      Environment.Exit(-1);
    }
  }
}
