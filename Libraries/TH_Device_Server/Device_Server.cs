﻿// Copyright (c) 2015 Feenux LLC, All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Data;

using TH_Configuration;
using TH_Global;
using TH_MySQL;
using TH_MySQL.Tables;
using TH_MTC_Data;
using TH_MTC_Requests;
using TH_Ping;
using TH_PlugIns_Server;

using TH_Device_Server.TableManagement;

namespace TH_Device_Server
{
    public class Device_Server
    {

        #region "Public"

        public Device_Server(Configuration config)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            updateConfigurationFile = true;

            LoadPlugins();

            configuration = config;

            RunningTime_Initialize();

            Ping_Agent_Initialize(config);
            Ping_DB_Initialize(config);
            Ping_PHP_Initialize(config);

            Status = ConnectionStatus.Stopped;

            ConnectionStatus_Initialize();
        }

        #region "Methods"

        public void Initialize()
        {
            System.Diagnostics.Stopwatch stpw = new System.Diagnostics.Stopwatch();
            stpw.Start();

            Requests_Initialize();

            stpw.Stop();
            Console.WriteLine("Initialize() : Requests_Initialize() : " + stpw.ElapsedMilliseconds + "ms");


            stpw = new System.Diagnostics.Stopwatch();
            stpw.Start();

            InitializeTables();

            stpw.Stop();
            Console.WriteLine("Initialize() : InitializeTables() : " + stpw.ElapsedMilliseconds + "ms");


            stpw = new System.Diagnostics.Stopwatch();
            stpw.Start();

            TablePlugIns_Initialize(configuration);

            stpw.Stop();
            Console.WriteLine("Initialize() : TablePlugIns_Initialize() : " + stpw.ElapsedMilliseconds + "ms");
        }

        public void Start()
        {
            start();
        }

        public void Start(bool startSampleFromFirst)
        {
            startFromFirst = startSampleFromFirst;
            start();
        }

        Thread worker;

        void start()
        {
            Console.WriteLine("Device_Server Started...");

            if (updateConfigurationFile && configurationPath != null)
            {
                if (File.Exists(configurationPath))
                {
                    Configuration lSettings = Configuration.ReadConfigFile(configurationPath);

                    if (lSettings != null)
                    {
                        configuration.Agent = lSettings.Agent;
                        configuration.Description = lSettings.Description;
                        configuration.FileLocations = lSettings.FileLocations;
                        configuration.SQL = lSettings.SQL;

                        FSW_Start();
                    }
                }
            }

            Global.Database_Create(configuration.SQL, configuration.SQL.Database);

            worker = new Thread(new ThreadStart(Worker_Start));
            worker.Start();

        }    

        public void Stop()
        {
            RunningTimeSTPW.Stop();

            if (worker != null) worker.Abort();

            //Requests_Stop();

            Status = ConnectionStatus.Stopped;

            Log("Device Server (" + configuration.Index.ToString() + ") Stopped");
        }

        public void Close()
        {
            Stop();

            TablePlugIns_Closing();

            if (Connection_Timer != null) Connection_Timer.Enabled = false;

            FSW_Stop();

            Log("Device (" + configuration.Index.ToString() + ") Closed");
        }

        #endregion

        #region "Properties"

        public Configuration configuration { get; set; }

        public string configurationPath { get; set; }

        public bool updateConfigurationFile { get; set; }

        #endregion

        #region "Events"

        #endregion

        #endregion

        #region "Worker Thread"

        void Worker_Start()
        {
            Connection_Initialize();
        }

        void Worker_Stop()
        {
            RunningTimeSTPW.Stop();

            Requests_Stop();

            Status = ConnectionStatus.Stopped;

            Log("CNC (" + configuration.Index.ToString() + ") Stopped");
        }

        #endregion

        #region "Connection"

        public System.Timers.Timer Connection_Timer;

        int TryCount = 1;
        const int ConnectionAttempts = 5;

        bool FirstAttempt = true;

        void Connection_Initialize()
        {
            Connection_Timer = new System.Timers.Timer();
            Connection_Timer.Elapsed += Connection_Timer_Elapsed;
            Connection_Timer.Interval = 1000;
            Connection_Timer.Enabled = true;
        }

        void Connection_Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Connection_Check();
        }

        void Connection_Check()
        {
            if (MTC_PingResult && SQL_PingResult && PHP_PingResult)
            {
                if (FirstAttempt) Log("Device Connected...");

                if (TryCount > 1) Log("Connection Reestablished");

                TryCount = 1;

                Connection_Timer.Interval = 1000;

                if (Status == ConnectionStatus.Stopped)
                {
                    Status = ConnectionStatus.Started;

                    System.Diagnostics.Stopwatch stpw = new System.Diagnostics.Stopwatch();
                    stpw.Start();

                    Initialize();

                    stpw.Stop();
                    Console.WriteLine("Connection_Check() : Intialize() : " + stpw.ElapsedMilliseconds + "ms");

                    RunningTimeSTPW.Start();

                    stpw = new System.Diagnostics.Stopwatch();
                    stpw.Start();

                    Requests_Start();

                    stpw.Stop();
                    Console.WriteLine("Connection_Check() : Requests_Start() : " + stpw.ElapsedMilliseconds + "ms");

                    Log("Device (" + configuration.Index.ToString() + ") Started...");
                }

                FirstAttempt = false;
            }
            else
            {
                if (!MTC_PingResult && !SQL_PingResult && !PHP_PingResult) Log("Device (" + configuration.Index.ToString() + ") MTC, SQL, and PHP Not Reachable!");
                else if (!MTC_PingResult && !SQL_PingResult) Log("Device (" + configuration.Index.ToString() + ") MTC and SQL Not Reachable!");
                else if (!MTC_PingResult && !PHP_PingResult) Log("Device (" + configuration.Index.ToString() + ") MTC and PHP Not Reachable!");
                else if (!PHP_PingResult && !SQL_PingResult) Log("Device (" + configuration.Index.ToString() + ") PHP and SQL Not Reachable!");
                else if (!MTC_PingResult) Log("Device (" + configuration.Index.ToString() + ") MTC Not Reachable!");
                else if (!PHP_PingResult) Log("Device (" + configuration.Index.ToString() + ") PHP Not Reachable!");
                else Log("Device (" + configuration.Index.ToString() + ") SQL Not Reachable!");


                if (Status == ConnectionStatus.Started || FirstAttempt)
                {
                    FirstAttempt = false;

                    Connection_Timer.Interval = 5000;

                    Log("Attempting to Connect...(Attempt #" + TryCount.ToString() + ")");

                    //if (!MTC_PingResult && !SQL_PingResult && !PHP_PingResult) Log("Device (" + configuration.Index.ToString() + ") MTC, SQL, and PHP Not Reachable!");
                    //else if (!MTC_PingResult && !SQL_PingResult) Log("Device (" + configuration.Index.ToString() + ") MTC and SQL Not Reachable!");
                    //else if (!MTC_PingResult && !PHP_PingResult) Log("Device (" + configuration.Index.ToString() + ") MTC and PHP Not Reachable!");
                    //else if (!PHP_PingResult && !SQL_PingResult) Log("Device (" + configuration.Index.ToString() + ") PHP and SQL Not Reachable!");
                    //else if (!MTC_PingResult) Log("Device (" + configuration.Index.ToString() + ") MTC Not Reachable!");
                    //else if (!PHP_PingResult) Log("Device (" + configuration.Index.ToString() + ") PHP Not Reachable!");
                    //else Log("Device (" + configuration.Index.ToString() + ") SQL Not Reachable!");

                    if (TryCount >= ConnectionAttempts)
                    {
                        TryCount = 1;

                        Connection_Timer.Interval = 1000;

                        Stop();
                    }

                    TryCount += 1;
                }
            }
        }

        #endregion

        #region "Connection Status"

        public enum ConnectionStatus
        {
            Stopped = 0,
            Started = 1
        }

        public ConnectionStatus Status;

        public delegate void StatusDelly(int Index, ConnectionStatus Status);
        public event StatusDelly StatusUpdated;

        void ConnectionStatus_Initialize()
        {
            ConnectionStatus_Timer = new System.Timers.Timer();
            ConnectionStatus_Timer.Interval = 1000;
            ConnectionStatus_Timer.Elapsed += ConnectionStatus_Timer_Elapsed;
            ConnectionStatus_Timer.Enabled = true;
        }

        void ConnectionStatus_Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            UpdateStatus(Status);
        }

        private void UpdateStatus(ConnectionStatus Status)
        {
            StatusDelly handler = StatusUpdated;
            if (handler != null) handler(configuration.Index, Status);
        }

        System.Timers.Timer ConnectionStatus_Timer;

        #endregion

        #region "Processing Status"

        string processingstatus;
        public string ProcessingStatus
        {
            get { return processingstatus; }
            set
            {
                processingstatus = value;
                if (ProcessingStatusChanged != null && configuration != null)
                {
                    ProcessingStatusChanged(configuration.Index, processingstatus);
                }       
            }
        }

        string prev_processingstatus;

        void UpdateProcessingStatus(string status)
        {
            prev_processingstatus = ProcessingStatus;
            ProcessingStatus = status;
        }

        void ClearProcessingStatus()
        {
            ProcessingStatus = prev_processingstatus;
            prev_processingstatus = "";
        }

        public delegate void ProcessingStatusChanged_Handler(int index, string status);
        public event ProcessingStatusChanged_Handler ProcessingStatusChanged;

        #endregion

        #region "Console Output"

        public void Log(string line)
        {
            Logger.Log(line);
        }

        #endregion

        #region "Exception Handler"

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log("UnhandledException : " + e.ExceptionObject.ToString());
        }

        #endregion

        #region "Ping"

        public bool MTC_PingResult = false;
        public bool SQL_PingResult = false;
        public bool PHP_PingResult = false;

        void Ping_Agent_MTCPingResult(bool PingResult) { MTC_PingResult = PingResult; }

        void Ping_DB_MySQLPingResult(bool PingResult) { SQL_PingResult = PingResult; }

        void Ping_PHP_PingResult(bool PingResult) { PHP_PingResult = PingResult; }


        void Ping_Agent_Initialize(Configuration lSettings)
        {
            PortPing Agent_Ping = new PortPing();
            Agent_Ping.Address = lSettings.Agent.IP_Address;
            Agent_Ping.Port = lSettings.Agent.Port;
            Agent_Ping.Interval = 2000;
            Agent_Ping.PingResult += Ping_Agent_MTCPingResult;
            Agent_Ping.Start();
        }

        void Ping_DB_Initialize(Configuration lSettings)
        {
            if (lSettings.SQL.PHP_Server == null)
            {
                MySQLPing DB_Ping = new MySQLPing();
                DB_Ping.Settings = lSettings.SQL;
                DB_Ping.MySQLPingResult += Ping_DB_MySQLPingResult;
                DB_Ping.Start();
            }
            else SQL_PingResult = true;
        }

        void Ping_PHP_Initialize(Configuration lSettings)
        {
            if (lSettings.SQL.PHP_Server != null)
            {
                PortPing PHP_Ping = new PortPing();
                PHP_Ping.Address = lSettings.SQL.PHP_Server;
                PHP_Ping.Port = 0;
                PHP_Ping.Interval = 2000;
                PHP_Ping.PingResult += Ping_PHP_PingResult;
                PHP_Ping.Start();
            }
            else PHP_PingResult = true;
        }

        #endregion

        #region "PlugIns"

        public IEnumerable<Lazy<Table_PlugIn>> TablePlugIns { get; set; }

        public List<Lazy<Table_PlugIn>> Table_Plugins { get; set; }

        TablePlugs TPLUGS;

        class TablePlugs
        {
            [ImportMany(typeof(Table_PlugIn))]
            public IEnumerable<Lazy<Table_PlugIn>> PlugIns { get; set; }
        }

        void LoadPlugins()
        {
            UpdateProcessingStatus("Loading Plugins...");

            string plugin_rootpath = FileLocations.TrakHound + @"\Server";

            if (!Directory.Exists(plugin_rootpath)) Directory.CreateDirectory(plugin_rootpath);

            Table_Plugins = new List<Lazy<Table_PlugIn>>();

            string pluginsPath;

            // Load from System Directory first (easier for user to navigate to 'C:\TrakHound\')
            pluginsPath = TH_Global.FileLocations.TrakHound + @"\Server\Plugins\";
            if (Directory.Exists(pluginsPath)) LoadTablePlugins(pluginsPath);

            // Load from App root Directory (doesn't overwrite plugins found in System Directory)
            pluginsPath = AppDomain.CurrentDomain.BaseDirectory + @"Plugins\";
            if (Directory.Exists(pluginsPath)) LoadTablePlugins(pluginsPath);

            TablePlugIns = Table_Plugins;

            ClearProcessingStatus();
        }

        void LoadTablePlugins(string Path)
        {
            Log("Searching for Table Plugins in '" + Path + "'");
            if (Directory.Exists(Path))
            {
                try
                {
                    TPLUGS = new TablePlugs();

                    var PageCatalog = new DirectoryCatalog(Path);
                    var PageContainer = new CompositionContainer(PageCatalog);
                    PageContainer.SatisfyImportsOnce(TPLUGS);

                    TablePlugIns = TPLUGS.PlugIns;

                    foreach (Lazy<Table_PlugIn> TP in TablePlugIns)
                    {
                        if (Table_Plugins.ToList().Find(x => x.Value.Name.ToLower() == TP.Value.Name.ToLower()) == null)
                        {
                            if (TP.IsValueCreated) Log(TP.Value.Name + " : PlugIn Found");
                            Table_Plugins.Add(TP);
                        }
                        else
                        {
                            if (TP.IsValueCreated) Log(TP.Value.Name + " : PlugIn Already Found");
                        }
                    }
                }
                catch (Exception ex) { Log("LoadTablePlugins() : Exception : " + ex.Message); }              

                // Search Subdirectories
                foreach (string directory in Directory.GetDirectories(Path))
                {
                    LoadTablePlugins(directory);
                }      
            }
            else Log("Table PlugIns Directory Doesn't Exist (" + Path + ")");
        }

        void TablePlugIns_Initialize(Configuration Config)
        {

            //Thread worker = new Thread(new ParameterizedThreadStart(TablePlugIns_Initialize_Worker));
            //worker.Start(Config);


            if (TablePlugIns != null)
            {
                IEnumerable<int> priorities = Table_Plugins.Select(x => x.Value.Priority).Distinct();

                List<int> sortedPriorities = priorities.ToList();
                sortedPriorities.Sort();

                foreach (int priority in sortedPriorities)
                {
                    List<Lazy<Table_PlugIn>> pluginsAtPriority = Table_Plugins.FindAll(x => x.Value.Priority == priority);

                    foreach (Lazy<Table_PlugIn> TP in pluginsAtPriority)
                    {
                        try
                        {
                            Table_PlugIn TPI = TP.Value;
                            TPI.DataEvent -= TPI_DataEvent;
                            TPI.DataEvent += TPI_DataEvent;
                            TPI.Initialize(Config);
                        }
                        catch (Exception ex)
                        {
                            Log("TablePlugIns_Initialize() : Exception : " + ex.Message);
                        }
                    }
                }
            }
        }

        void TablePlugIns_Initialize_Worker(object config)
        {
            Configuration Config = (Configuration)config;

            if (TablePlugIns != null)
            {
                IEnumerable<int> priorities = Table_Plugins.Select(x => x.Value.Priority).Distinct();

                List<int> sortedPriorities = priorities.ToList();
                sortedPriorities.Sort();

                foreach (int priority in sortedPriorities)
                {
                    List<Lazy<Table_PlugIn>> pluginsAtPriority = Table_Plugins.FindAll(x => x.Value.Priority == priority);

                    foreach (Lazy<Table_PlugIn> TP in pluginsAtPriority)
                    {
                        try
                        {
                            Table_PlugIn TPI = TP.Value;
                            TPI.DataEvent -= TPI_DataEvent;
                            TPI.DataEvent += TPI_DataEvent;
                            TPI.Initialize(Config);
                        }
                        catch (Exception ex)
                        {
                            Log("TablePlugIns_Initialize() : Exception : " + ex.Message);
                        }
                    }
                }
            }
        }

        void TablePlugIns_Update_Probe(TH_MTC_Data.Components.ReturnData returnData)
        {
            if (TablePlugIns != null)
            {
                IEnumerable<int> priorities = Table_Plugins.Select(x => x.Value.Priority).Distinct();

                List<int> sortedPriorities = priorities.ToList();
                sortedPriorities.Sort();

                foreach (int priority in sortedPriorities)
                {
                    List<Lazy<Table_PlugIn>> pluginsAtPriority = Table_Plugins.FindAll(x => x.Value.Priority == priority);

                    foreach (Lazy<Table_PlugIn> TP in pluginsAtPriority)
                    {
                        Table_PlugIn TPI = TP.Value;
                        TPI.Update_Probe(returnData);
                    }
                }
            }
        }

        void TablePlugIns_Update_Current(TH_MTC_Data.Streams.ReturnData returnData)
        {
            if (TablePlugIns != null)
            {
                IEnumerable<int> priorities = Table_Plugins.Select(x => x.Value.Priority).Distinct();

                List<int> sortedPriorities = priorities.ToList();
                sortedPriorities.Sort();

                foreach (int priority in sortedPriorities)
                {
                    List<Lazy<Table_PlugIn>> pluginsAtPriority = Table_Plugins.FindAll(x => x.Value.Priority == priority);

                    foreach (Lazy<Table_PlugIn> TP in pluginsAtPriority)
                    {
                        Table_PlugIn TPI = TP.Value;
                        TPI.Update_Current(returnData);
                    }
                }
            }
        }

        void TablePlugIns_Update_Sample(TH_MTC_Data.Streams.ReturnData returnData)
        {
            Thread worker = new Thread(new ParameterizedThreadStart(TablePlugIns_Update_Sample_Worker));
            worker.Start(returnData);
        }

        void TablePlugIns_Update_Sample_Worker(object returnData)
        {
            TH_MTC_Data.Streams.ReturnData rd = (TH_MTC_Data.Streams.ReturnData)returnData;

            if (TablePlugIns != null)
            {
                IEnumerable<int> priorities = Table_Plugins.Select(x => x.Value.Priority).Distinct();

                List<int> sortedPriorities = priorities.ToList();
                sortedPriorities.Sort();

                foreach (int priority in sortedPriorities)
                {
                    List<Lazy<Table_PlugIn>> pluginsAtPriority = Table_Plugins.FindAll(x => x.Value.Priority == priority);

                    foreach (Lazy<Table_PlugIn> TP in pluginsAtPriority)
                    {
                        try
                        {
                            Table_PlugIn TPI = TP.Value;
                            TPI.Update_Sample(rd);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Plugin Exception!");
                        }
                    }
                }
            }
        }

        void TPI_DataEvent(DataEvent_Data de_data)
        {
            if (TablePlugIns != null)
            {
                IEnumerable<int> priorities = Table_Plugins.Select(x => x.Value.Priority).Distinct();

                List<int> sortedPriorities = priorities.ToList();
                sortedPriorities.Sort();

                foreach (int priority in sortedPriorities)
                {
                    List<Lazy<Table_PlugIn>> pluginsAtPriority = Table_Plugins.FindAll(x => x.Value.Priority == priority);

                    foreach (Lazy<Table_PlugIn> TP in pluginsAtPriority)
                    {
                        Table_PlugIn TPI = TP.Value;
                        TPI.Update_DataEvent(de_data);
                    }
                }
            }
        }

        void TablePlugIns_Closing()
        {
            if (TablePlugIns != null)
            {
                IEnumerable<int> priorities = Table_Plugins.Select(x => x.Value.Priority).Distinct();

                List<int> sortedPriorities = priorities.ToList();
                sortedPriorities.Sort();

                foreach (int priority in sortedPriorities)
                {
                    List<Lazy<Table_PlugIn>> pluginsAtPriority = Table_Plugins.FindAll(x => x.Value.Priority == priority);

                    foreach (Lazy<Table_PlugIn> TP in pluginsAtPriority)
                    {
                        Table_PlugIn TPI = TP.Value;
                        TPI.Closing();
                    }
                }
            }
        }

        #endregion

        #region "MTC Requests"

        void Requests_Initialize()
        {
            Probe_Initialize();
            Current_Initialize();
            Sample_Initialize();
        }

        void Requests_Start()
        {
            Probe_Start();
            Current_Start();
        }

        void Requests_Stop()
        {
            Probe_Stop();
            Current_Stop();
            Sample_Stop();
        }

        #region "Probe"

        Probe probe;

        TH_MTC_Data.Components.ReturnData ProbeData;

        void Probe_Initialize()
        {
            probe = new Probe();
            probe.configuration = configuration;
            probe.ProbeFinished -= probe_ProbeFinished;   
            probe.ProbeFinished += probe_ProbeFinished;          
        }

        void Probe_Start()
        {
            UpdateProcessingStatus("Running Probe Request..");
            if (probe != null) probe.Start();
        }

        void Probe_Stop()
        {
            if (probe != null) probe.Stop();
        }

        void probe_ProbeFinished(TH_MTC_Data.Components.ReturnData returnData)
        {
            UpdateProcessingStatus("Probe Received");

            ProbeData = returnData;

            if (configuration.Server.Tables.MTConnect.Sample)
            {
                //CreateSampleTables_MySQL(returnData.DS);
            }

            TablePlugIns_Update_Probe(returnData);

            ClearProcessingStatus();
        }

        #endregion

        #region "Current"

        Current current;

        int sampleInterval;
        int sampleCounter = -1;

        Int64 Agent_ID;
        DateTime Agent_First;
        DateTime Agent_Last;

        void Current_Initialize()
        {
            Agent_First = DateTime.MinValue;
            Agent_Last = DateTime.MinValue;

            current = new Current();
            current.configuration = configuration;
            current.CurrentFinished -= current_CurrentFinished;
            current.CurrentFinished += current_CurrentFinished;

            // Calculate interval to use for when to run a Sample
            sampleInterval = configuration.Agent.Sample_Heartbeat / configuration.Agent.Current_Heartbeat;
        }

        void Current_Start()
        {
            UpdateProcessingStatus("Running Current Request..");
            if (current != null) current.Start(configuration.Agent.Current_Heartbeat);
        }

        void Current_Stop()
        {
            if (current != null) current.Stop();

            sampleCounter = -1;
        }

        void current_CurrentFinished(TH_MTC_Data.Streams.ReturnData returnData)
        {
            UpdateProcessingStatus("Current Received");

            // Update Agent_Info
            if (Agent_ID != returnData.header.instanceId)
            {
                Agent_First = returnData.header.creationTime;
            }
            Agent_Last = returnData.header.creationTime;
            if (Agent_Last < Agent_First) Agent_Last = Agent_First;
            Agent_ID = returnData.header.instanceId;

            // Update all of the PlugIns with the ReturnData object
            TablePlugIns_Update_Current(returnData);

            sampleCounter += 1;

            if (sampleCounter >= sampleInterval || (sampleCounter <= 0 || configuration.Agent.Simulation_Sample_Path != null))
            {
                if (!inProgress)
                {
                    Sample_Start(returnData.header);
                    sampleCounter = 0;
                }
                //else Log("Previous Sample still in Progress : Sample Skipped!");
            }

            ClearProcessingStatus();
        }

        #endregion

        #region "Sample"

        bool startFromFirst = true;

        Sample sample;

        Int64 lastSequenceSampled = -1;
        Int64 agentInstanceId = -1;

        bool inProgress = false;

        void Sample_Initialize()
        {
            lastSequenceSampled = GetLastSequenceFromMySQL();

            agentInstanceId = GetAgentInstanceIdFromMySQL();
        }

        Int64 GetLastSequenceFromMySQL()
        {
            Int64 Result = -1;

            Variables.VariableData vd = Variables.Get(configuration.SQL, "last_sequence_sampled");
            if (vd != null)
            {
                Int64.TryParse(vd.value, out Result);
            }               

            return Result;
        }

        Int64 GetAgentInstanceIdFromMySQL()
        {
            Int64 Result = -1;

            Variables.VariableData vd = Variables.Get(configuration.SQL, "agent_instanceid");
            if (vd != null)
            {
                Int64.TryParse(vd.value, out Result);
            }

            return Result;
        }

        const Int64 MaxSampleCount = 100000;

        void Sample_Start(TH_MTC_Data.Header_Streams header)
        {
            UpdateProcessingStatus("Running Sample Request..");

            sample = new Sample();
            sample.configuration = configuration;
            sample.SampleFinished -= sample_SampleFinished;
            sample.SampleFinished += sample_SampleFinished;

            if (sample != null)
            {

                // Check/Update Agent Instance Id -------------------
                Int64 lastInstanceId = agentInstanceId;
                agentInstanceId = header.instanceId;
                Variables.Update(configuration.SQL, "Agent_InstanceID", agentInstanceId.ToString(), header.creationTime);
                // --------------------------------------------------

                // Get Sequence Number to use -----------------------
                Int64 First = header.firstSequence;
                if (!startFromFirst)
                {
                    First = header.lastSequence;
                    startFromFirst = true;
                }
                else if (lastInstanceId == agentInstanceId && lastSequenceSampled > 0 && lastSequenceSampled >= header.firstSequence)
                {
                    First = lastSequenceSampled + 1;
                }
                else if (First > 0)
                {
                    Int64 first = First;

                    // Increment some sequences since the Agent might change the first sequence
                    // before the Sample request gets read
                    // (should be fixed in Agent to automatically read the last first 'available' sequence
                    // instead of returning an error)
                    First += 20;
                    Log("First = " + First.ToString() + " : " + first.ToString());
                }
                    
                // Get Last Sequence Number available from Header
                Int64 Last = header.lastSequence;

                // Calculate Sample count
                Int64 SampleCount = Last - First;
                if (SampleCount > MaxSampleCount)
                {
                    SampleCount = MaxSampleCount;
                    Last = First + MaxSampleCount;
                }

                // Update Last Sequence Sampled for the subsequent samples
                lastSequenceSampled = Last;
                Variables.Update(configuration.SQL, "Last_Sequence_Sampled", Last.ToString(), header.creationTime);


                if (configuration.Agent.Simulation_Sample_Path != null)
                {
                    Log("Sample_Start() : Simulation File : " + configuration.Agent.Simulation_Sample_Path);
                    sample.Start(null, First, SampleCount);
                }
                else if (SampleCount > 0)
                {
                    Log("Sample_Start() : " + First.ToString() + " to " + Last.ToString());
                    sample.Start(null, First, SampleCount);
                }
                else
                {
                    Log("Sample Skipped : No New Data! : " + First.ToString() + " to " + Last.ToString());

                    // Update all of the Table Plugins with a Null ReturnData
                    TablePlugIns_Update_Sample(null);
                }
                    
            }
        }

        void Sample_Stop()
        {
            if (sample != null) sample.Stop();
        }

        void sample_SampleFinished(TH_MTC_Data.Streams.ReturnData returnData)
        {
            UpdateProcessingStatus("Sample Received..");

            inProgress = true;

            Log("Device_Server Received Sample : " + DateTime.Now.ToLongTimeString() + "." + DateTime.Now.Millisecond.ToString());

            // Update all of the Table Plugins with the ReturnData
            TablePlugIns_Update_Sample(returnData);

            // Create/Update Individual Sample tables in MySQL
            //if (configuration.Server.Tables.MTConnect.Sample) ProcessSampleTables(returnData);

            inProgress = false;

            ClearProcessingStatus();
        }

        #endregion

        #endregion

        #region "Running Time"

        System.Diagnostics.Stopwatch RunningTimeSTPW;
        System.Timers.Timer RunningTime_TIMER;
        public TimeSpan RunningTime;

        void RunningTime_Initialize()
        {
            RunningTimeSTPW = new System.Diagnostics.Stopwatch();
            RunningTime_TIMER = new System.Timers.Timer();
            RunningTime_TIMER.Interval = 500;
            RunningTime_TIMER.Elapsed += RunningTime_TIMER_Elapsed;
            RunningTime_TIMER.Enabled = true;
        }

        void RunningTime_TIMER_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            RunningTime = RunningTimeSTPW.Elapsed;
        }

        #endregion

        #region "Settings File Watcher"

        FileSystemWatcher FSW;

        bool ReadNewSettingsFile;

        void FSW_Initialize()
        {
            if (configurationPath != null)
            {

                FSW = new FileSystemWatcher();
                FSW.Path = Path.GetDirectoryName(configurationPath);
                FSW.Filter = Path.GetFileName(configurationPath);

                Log("FSW.Path = " + FSW.Path);
                Log("FSW.Filter = " + FSW.Filter);

                FSW.Changed += FSW_Changed;

            }
        }

        void FSW_Start()
        {
            if (FSW != null) FSW.EnableRaisingEvents = true;
        }

        void FSW_Stop()
        {
            if (FSW != null) FSW.EnableRaisingEvents = false;
        }

        void FSW_Changed(object sender, FileSystemEventArgs e)
        {
            Log(configuration.Description.Description + " Settings File Changed!");
            Log("Reloading Settings File from \"" + configurationPath + "\"");

            FSW_Stop();

            ReadNewSettingsFile = true;

            Stop();
        }

        void FSW_WaitForStop()
        {
            System.Diagnostics.Stopwatch Timeout = new System.Diagnostics.Stopwatch();
            Timeout.Start();

            Timeout.Stop();
        }

        #endregion

        #region "Table Management"

        void InitializeTables()
        {
            Variables.CreateTable(configuration.SQL);
            //AgentInfo.Initialize(configuration);
        }

        //#region "Samples"

        //DataSet CreateIndividualSampleTables(DataSet DS)
        //{
        //    DataSet Result = new DataSet();

        //    foreach (DataTable DT in DS.Tables)
        //    {
        //        DataView DV = DT.AsDataView();

        //        DataTable Distinct_DT = DV.ToTable(true, "dataItemId");

        //        foreach (DataRow Row in Distinct_DT.Rows)
        //        {
        //            if (Row.Table.Columns.Contains("dataItemId"))
        //            {
        //                DV = DT.AsDataView();

        //                DV.RowFilter = "dataItemId = '" + Row["dataItemId"].ToString() + "'";

        //                DataTable Variable_DT = DV.ToTable();
        //                Variable_DT.TableName = Row["dataItemId"].ToString();

        //                Result.Tables.Add(Variable_DT);
        //            }
        //        }
        //    }

        //    return Result;
        //}

        //void CreateSampleTables_MySQL(DataSet DS)
        //{
        //    List<string> TableNames = new List<string>();
        //    foreach (DataTable DT in DS.Tables)
        //    {
        //        foreach (DataRow Row in DT.Rows)
        //        {
        //            if (DT.Columns.Contains("id")) TableNames.Add("SAMPLE_" + Row["id"].ToString().ToUpper());
        //        }
        //    }
                
        //    bool TablesExist = Global.TableExists(configuration.SQL, TableNames);

        //}

        //void ProcessSampleTables(TH_MTC_Data.Streams.ReturnData returnData)
        //{
        //    if (returnData.deviceStream != null)
        //    {
        //        TH_MTC_Data.Streams.DataItemCollection dataItems = TH_MTC_Data.Streams.Tools.GetDataItemsFromDeviceStream(returnData.deviceStream);

        //        if (ProbeData != null)
        //        {
        //            TH_MTC_Data.Components.DataItemCollection Probe_dataItems = TH_MTC_Data.Components.Tools.GetDataItemsFromDevice(ProbeData.device);

        //            foreach (TH_MTC_Data.Components.DataItem DI in Probe_dataItems.Samples)
        //            {
        //                List<TH_MTC_Data.Streams.Sample> SampleTest = dataItems.Samples.FindAll(x => x.dataItemId.ToLower() == DI.id.ToLower());

        //                Log(DI.id + " count = " + SampleTest.Count.ToString());
        //            }
        //        }
        //    }
        //}

        //#endregion

        #endregion

    }
}
