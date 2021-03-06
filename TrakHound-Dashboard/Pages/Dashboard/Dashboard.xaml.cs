﻿// Copyright (c) 2016 Feenux LLC, All Rights Reserved.

// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using TrakHound;
using TrakHound.Configurations;
using TrakHound.Logging;
using TrakHound.Plugins.Client;
using TrakHound.Tools;
using TrakHound_UI;

namespace TrakHound_Dashboard.Pages.Dashboard
{
    /// <summary>
    /// Interaction logic for DashboardPage.xaml
    /// </summary>
    public partial class Dashboard : UserControl, IClientPlugin
    {
        private System.Timers.Timer timespanUpdateTimer;


        public string Title { get { return "Dashboard"; } }

        public string Description { get { return "Contains and organizes pages for displaying Device data in various ways. Acts as the Home page for other Device Monitoring Plugins."; } }

        public Uri Image { get { return new Uri("pack://application:,,,/TrakHound-Dashboard;component/Resources/Dashboard_01.png"); } }

        public bool ZoomEnabled { get { return true; } }

        public void SetZoom(double zoomPercentage)
        {
            if (PageContent != null)
            {
                var page = PageContent as IPage;

                double zoomLevel = MainWindow.LoadSavedPageZoomValue(page);
                zoomLevel += zoomPercentage;

                if (zoomPercentage > 0) zoomLevel = Math.Min(2, zoomLevel);
                else if (zoomPercentage < 0) zoomLevel = Math.Max(0.25, zoomLevel);
                else zoomLevel = 1;

                MainWindow.SavePageZoomLevel(page, zoomLevel);

                ZoomLevel = zoomLevel;
            }
        }

        public void Opened() { }
        public bool Opening() { return true; }

        public void Closed() { }
        public bool Closing() { return true; }

        public event SendData_Handler SendData;

        public string ParentPlugin { get { return null; } }
        public string ParentPluginCategory { get { return null; } }

        public bool OpenOnStartUp { get { return true; } }

        public List<PluginConfigurationCategory> SubCategories { get; set; }

        public List<IClientPlugin> Plugins { get; set; }

        public IPage Options { get; set; }

        private ObservableCollection<DeviceDescription> _devices;
        public ObservableCollection<DeviceDescription> Devices
        {
            get
            {
                if (_devices == null)
                {
                    _devices = new ObservableCollection<DeviceDescription>();
                }
                return _devices;
            }
            set
            {
                _devices = value;
            }
        }

        private ObservableCollection<ListButton> _pages;
        public ObservableCollection<ListButton> Pages
        {
            get
            {
                if (_pages == null)
                    _pages = new ObservableCollection<ListButton>();
                return _pages;
            }

            set
            {
                _pages = value;
            }
        }

        private ObservableCollection<DateTime> _dateTimes;
        public ObservableCollection<DateTime> DateTimes
        {
            get
            {
                if (_dateTimes == null)
                    _dateTimes = new ObservableCollection<DateTime>();
                return _dateTimes;
            }

            set
            {
                _dateTimes = value;
            }
        }


        private PluginConfiguration currentPage;

        private List<PluginConfiguration> enabledPlugins;

        #region "Dependency Properties"

        public object PageContent
        {
            get { return (object)GetValue(PageContentProperty); }
            set { SetValue(PageContentProperty, value); }
        }

        public static readonly DependencyProperty PageContentProperty =
            DependencyProperty.Register("PageContent", typeof(object), typeof(Dashboard), new PropertyMetadata(null));

        public double ZoomLevel
        {
            get { return (double)GetValue(ZoomLevelProperty); }
            set { SetValue(ZoomLevelProperty, value); }
        }

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register("ZoomLevel", typeof(double), typeof(Dashboard), new PropertyMetadata(1D));


        public bool LoggedIn
        {
            get { return (bool)GetValue(LoggedInProperty); }
            set { SetValue(LoggedInProperty, value); }
        }

        public static readonly DependencyProperty LoggedInProperty =
            DependencyProperty.Register("LoggedIn", typeof(bool), typeof(Dashboard), new PropertyMetadata(false));


        public bool LoadingDevices
        {
            get { return (bool)GetValue(LoadingDevicesProperty); }
            set { SetValue(LoadingDevicesProperty, value); }
        }

        public static readonly DependencyProperty LoadingDevicesProperty =
            DependencyProperty.Register("LoadingDevices", typeof(bool), typeof(Dashboard), new PropertyMetadata(false));


        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register("IsExpanded", typeof(bool), typeof(Dashboard), new PropertyMetadata(true));


        public DateTime From
        {
            get { return (DateTime)GetValue(FromProperty); }
            set { SetValue(FromProperty, value); }
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(DateTime), typeof(Dashboard), new PropertyMetadata(DateTime.MinValue));

        public DateTime To
        {
            get { return (DateTime)GetValue(ToProperty); }
            set { SetValue(ToProperty, value); }
        }

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(DateTime), typeof(Dashboard), new PropertyMetadata(DateTime.MinValue));


        public string CurrentDate
        {
            get { return (string)GetValue(CurrentDateProperty); }
            set { SetValue(CurrentDateProperty, value); }
        }

        public static readonly DependencyProperty CurrentDateProperty =
            DependencyProperty.Register("CurrentDate", typeof(string), typeof(Dashboard), new PropertyMetadata(null));

        public bool DateMenuShown
        {
            get { return (bool)GetValue(DateMenuShownProperty); }
            set { SetValue(DateMenuShownProperty, value); }
        }

        public static readonly DependencyProperty DateMenuShownProperty =
            DependencyProperty.Register("DateMenuShown", typeof(bool), typeof(Dashboard), new PropertyMetadata(false));


        public DeviceComparisonTypes ComparisonType
        {
            get { return (DeviceComparisonTypes)GetValue(ComparisonTypeProperty); }
            set { SetValue(ComparisonTypeProperty, value); }
        }


        public static readonly DependencyProperty ComparisonTypeProperty =
            DependencyProperty.Register("ComparisonType", typeof(DeviceComparisonTypes), typeof(Dashboard), new PropertyMetadata(DeviceComparisonTypes.INDEX));

        #endregion


        public Dashboard()
        {
            InitializeComponent();
            root.DataContext = this;

            SubCategories = new List<PluginConfigurationCategory>();
            var pages = new PluginConfigurationCategory();
            pages.Name = "Pages";
            SubCategories.Add(pages);

            IsExpanded = Properties.Settings.Default.DashboardIsExpanded;

            timespanUpdateTimer = new System.Timers.Timer();
            timespanUpdateTimer.Interval = 5000;
            timespanUpdateTimer.Elapsed += TimespanUpdateTimer_Elapsed;
            timespanUpdateTimer.Enabled = true;
        }

        private void TimespanUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateDashboardTimespan), System.Windows.Threading.DispatcherPriority.Background, new object[] { });
        }

        public void Initialize()
        {
            enabledPlugins = new List<PluginConfiguration>();

            foreach (PluginConfigurationCategory category in SubCategories)
            {
                foreach (PluginConfiguration config in category.PluginConfigurations)
                {
                    config.EnabledChanged += config_EnabledChanged;

                    if (config.Enabled) Plugins_Load(config);
                }
            }

            LoadDashboardTimespan();

            // Load Comparison Type
            var type = LoadComparisonType();
            UpdateComparisonType(type);
            ComparisonType = type;
        }

        private void UpdateDashboardTimespan()
        {
            try
            {
                // Load Current Day Start and Day End
                var d = DateTime.Now;
                var dayStart = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Local);
                var dayEnd = dayStart.AddDays(1);

                DateTime from = DateTime.MinValue;
                DateTime to = DateTime.MinValue;

                // Set From
                if (From > dayStart) from = From;
                else if (From > DateTime.MinValue) from = new DateTime(dayStart.Year, dayStart.Month, dayStart.Day, From.Hour, 0, 0);
                else from = dayStart;

                // Set To
                if (To > dayStart && To <= dayEnd) to = To;
                else if (To > DateTime.MinValue && To.Hour > 0) to = new DateTime(dayStart.Year, dayStart.Month, dayStart.Day, To.Hour, 0, 0);
                else to = dayEnd;

                // Update Timespan
                if (From != from || To != to)
                {
                    DateTimes.Clear();
                    for (var x = 0; x <= 24; x++) DateTimes.Add(dayStart.AddHours(x));

                    From = from;
                    To = to;

                    SaveDashboardTimespan();
                    SendDashboardTimespan();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("UpdateDashboardTimespan :: Exception :: " + ex.Message);
            }
        }

        private void LoadDashboardTimespan()
        {
            try
            {
                // Load Current Day Start and Day End
                var d = DateTime.Now;
                var dayStart = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Local);
                var dayEnd = dayStart.AddDays(1);

                DateTimes.Clear();
                for (var x = 0; x <= 24; x++) DateTimes.Add(dayStart.AddHours(x));

                // Load From Setting
                string fromSetting = Properties.Settings.Default.SavedStatusFromTime;
                DateTime savedFrom = DateTime.MinValue;
                DateTime.TryParse(fromSetting, out savedFrom);

                // Load To Setting
                string toSetting = Properties.Settings.Default.SavedStatusToTime;
                DateTime savedTo = DateTime.MinValue;
                DateTime.TryParse(toSetting, out savedTo);

                // Set From
                if (savedFrom > dayStart) From = savedFrom;
                else if (savedFrom > DateTime.MinValue) From = new DateTime(dayStart.Year, dayStart.Month, dayStart.Day, savedFrom.Hour, 0, 0);
                else From = dayStart;

                // Set To
                if (savedTo > dayStart && savedTo < dayEnd && savedTo > From) To = savedTo;
                else if (savedTo > DateTime.MinValue && savedTo.Hour > 0) To = new DateTime(dayStart.Year, dayStart.Month, dayStart.Day, savedTo.Hour, 0, 0);
                else To = dayEnd;

                SaveDashboardTimespan();
                SendDashboardTimespan();
            }
            catch (Exception ex)
            {
                Logger.Log("LoadDashboardTimespan :: Exception :: " + ex.Message);
            }        
        }
        
        internal void SendDashboardTimespan()
        {
            SendDashboardTimespan(From, To);
        }

        internal void SendDashboardTimespan(DateTime from, DateTime to)
        {
            var data = new EventData(this);
            data.Id = "DASHBOARD_TIMESPAN";
            data.Data01 = from;
            data.Data02 = to;
            SendData?.Invoke(data);
        }

        internal void SaveDashboardTimespan()
        {
            Properties.Settings.Default.SavedStatusFromTime = From.ToString();
            Properties.Settings.Default.SavedStatusToTime = To.ToString();
            Properties.Settings.Default.Save();
        }

        internal void SaveDashboardFromTime()
        {
            Properties.Settings.Default.SavedStatusFromTime = From.ToString();
            Properties.Settings.Default.Save();
        }

        internal void SaveDashboardToTime()
        {
            Properties.Settings.Default.SavedStatusToTime = To.ToString();
            Properties.Settings.Default.Save();
        }

        public void GetSentData(EventData data)
        {
            Dispatcher.BeginInvoke(new Action(UpdateCurrentDate), System.Windows.Threading.DispatcherPriority.Background, new object[] { });

            Dispatcher.BeginInvoke(new Action<EventData>(UpdateLoggedInChanged), System.Windows.Threading.DispatcherPriority.Normal, new object[] { data });
            Dispatcher.BeginInvoke(new Action<EventData>(UpdateDevicesLoading), System.Windows.Threading.DispatcherPriority.Normal, new object[] { data });

            Dispatcher.BeginInvoke(new Action<EventData>(UpdateDeviceAdded), System.Windows.Threading.DispatcherPriority.DataBind, new object[] { data });
            Dispatcher.BeginInvoke(new Action<EventData>(UpdateDeviceUpdated), System.Windows.Threading.DispatcherPriority.DataBind, new object[] { data });
            Dispatcher.BeginInvoke(new Action<EventData>(UpdateDeviceRemoved), System.Windows.Threading.DispatcherPriority.DataBind, new object[] { data });

            if (Plugins != null)
            {
                foreach (IClientPlugin plugin in Plugins)
                {
                    var sendDataInfo = new SendDataInfo(plugin, data);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(ProcessSendData), sendDataInfo);
                }
            }
        }

        private class SendDataInfo
        {
            public SendDataInfo(IClientPlugin plugin, EventData data)
            {
                Plugin = plugin;
                Data = data;
            }

            public IClientPlugin Plugin { get; set; }
            public EventData Data { get; set; }
        }

        private void ProcessSendData(object o)
        {
            if (o != null)
            {
                var sendDataInfo = (SendDataInfo)o;

                try
                {
                    sendDataInfo.Plugin.GetSentData(sendDataInfo.Data);
                }
                catch (Exception ex) { Logger.Log("Plugin Error :: " + ex.Message); }
            }
        }


        private void UpdateCurrentDate()
        {
            CurrentDate = DateTime.Now.ToShortDateString();
        }

        void UpdateLoggedInChanged(EventData data)
        {
            if (data != null)
            {
                if (data.Id.ToLower() == "userloggedin")
                {
                    LoggedIn = true;
                    Devices.Clear();
                }

                if (data.Id.ToLower() == "userloggedout")
                {
                    LoggedIn = false;
                    Devices.Clear();
                }
            }
        }

        void UpdateDevicesLoading(EventData data)
        {
            if (data != null)
            {
                if (data.Id == "DEVICES_LOADING")
                {
                    LoadingDevices = true;
                    Devices.Clear();
                }

                if (data.Id == "DEVICES_LOADED")
                {
                    LoadingDevices = false;
                }
            }
        }

        void UpdateDeviceAdded(EventData data)
        {
            if (data != null)
            {
                if (data.Id == "DEVICE_ADDED" && data.Data01 != null)
                {
                    var device = (DeviceDescription)data.Data01;
                    if (device.Enabled) Devices.Add(device);
                }
            }
        }

        void UpdateDeviceUpdated(EventData data)
        {
            if (data != null)
            {
                if (data.Id == "DEVICE_UPDATED" && data.Data01 != null)
                {
                    var device = (DeviceDescription)data.Data01;

                    int i = Devices.ToList().FindIndex(x => x.UniqueId == device.UniqueId);
                    if (i >= 0)
                    {
                        if (device.Enabled) Devices[i] = device;
                        else Devices.RemoveAt(i);
                    }
                    else if (device.Enabled) Devices.Add(device);
                }
            }
        }

        void UpdateDeviceRemoved(EventData data)
        {
            if (data != null)
            {
                if (data.Id == "DEVICE_REMOVED" && data.Data01 != null)
                {
                    var device = (DeviceDescription)data.Data01;

                    int i = Devices.ToList().FindIndex(x => x.UniqueId == device.UniqueId);
                    if (i >= 0)
                    {
                        Devices.RemoveAt(i);
                    }
                }
            }
        }
        
        private void OpenDeviceManager_Clicked(TrakHound_UI.Button bt)
        {
            var data = new EventData(this);
            data.Id = "SHOW_DEVICE_MANAGER";
            SendData(data);
        }

        #region "Child PlugIns"

        public void Plugins_Load(PluginConfiguration config)
        {
            if (Plugins != null)
            {
                if (!enabledPlugins.Contains(config))
                {
                    IClientPlugin plugin = Plugins.Find(x => x.Title.ToUpper() == config.Name.ToUpper());
                    if (plugin != null)
                    {
                        try
                        {
                            plugin.SubCategories = config.SubCategories;
                            plugin.SendData += Plugin_SendData;
                            
                            plugin.Initialize();
                        }
                        catch { }

                        var bt = new ListButton();
                        if (plugin.Image != null) bt.Image = new BitmapImage(plugin.Image);
                        bt.Text = plugin.Title;
                        bt.Selected += PageSelected;
                        bt.DataObject = plugin;
                        Pages.Add(bt);

                        SortPageList();

                        enabledPlugins.Add(config);
                    }
                }
            }
        }

        private void SortPageList()
        {
            Pages.Sort();

            int index = Pages.ToList().FindIndex(x => x.Text == "Overview");
            if (index >= 0)
            {
                var overview = Pages[index];
                Pages.RemoveAt(index);
                Pages.Insert(0, overview);
            }
        }

        void AddSubPlugins(IClientPlugin plugin)
        {
            plugin.Plugins = new List<IClientPlugin>();

            if (plugin.SubCategories != null)
            {
                foreach (PluginConfigurationCategory subcategory in plugin.SubCategories)
                {
                    foreach (PluginConfiguration subConfig in subcategory.PluginConfigurations)
                    {
                        IClientPlugin cplugin = Plugins.Find(x => x.Title.ToUpper() == subConfig.Name.ToUpper());
                        if (cplugin != null)
                        {
                            plugin.Plugins.Add(cplugin);
                        }
                    }
                }
            }
        }

        void Plugin_SendData(EventData data)
        {
            SendData?.Invoke(data);
        }

        public void Plugins_Unload(PluginConfiguration config)
        {
            if (config != null)
            {
                if (!config.Enabled)
                {
                    ListButton lb = Pages.ToList().Find(x => GetPluginName(x.Text) == GetPluginName(config.Name));
                    if (lb != null)
                    {
                        Pages.Remove(lb);
                    }

                    if (config == currentPage) PageContent = null;

                    if (enabledPlugins.Contains(config)) enabledPlugins.Remove(config);
                }
            }
        }

        static string GetPluginName(string s)
        {
            if (s != null) return s.ToUpper();
            return s;
        }

        private void PageSelected(ListButton lb)
        {
            StopSlideshow();

            SelectPage(lb);
        }

        void config_EnabledChanged(PluginConfiguration config)
        {
            if (config.Enabled) Plugins_Load(config);
            else Plugins_Unload(config);
        }

        #endregion

        #region "Slideshow"

        public bool SlideshowRunning
        {
            get { return (bool)GetValue(SlideshowRunningProperty); }
            set { SetValue(SlideshowRunningProperty, value); }
        }

        public static readonly DependencyProperty SlideshowRunningProperty =
            DependencyProperty.Register("SlideshowRunning", typeof(bool), typeof(Dashboard), new PropertyMetadata(false));

        private void StartSlideshow_Clicked(TrakHound_UI.Button bt)
        {
            StartSlideshow();
        }

        private void StopSlideshow_Clicked(TrakHound_UI.Button bt)
        {
            StopSlideshow();
        }

        private System.Timers.Timer slideshowTimer;

        private void StartSlideshow()
        {
            SlideshowRunning = true;

            slideshowTimer = new System.Timers.Timer();
            slideshowTimer.Interval = 20000;
            slideshowTimer.Elapsed += SlideshowTimer_Elapsed;
            slideshowTimer.Enabled = true;
        }

        private void SlideshowTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => {

                SelectNextPage();

            }), System.Windows.Threading.DispatcherPriority.Background, new object[] { });
        }

        private void StopSlideshow()
        {
            SlideshowRunning = false;

            if (slideshowTimer != null) slideshowTimer.Enabled = false;
            slideshowTimer = null;
        }

        #endregion

        #region "Sort"

        private DeviceComparisonTypes LoadComparisonType()
        {
            var p = Properties.Settings.Default.DashboardComparisonType;
            if (!string.IsNullOrEmpty(p))
            {
                DeviceComparisonTypes type;
                if (Enum.TryParse<DeviceComparisonTypes>(p, out type)) return type;
            }

            return DeviceComparisonTypes.INDEX;
        }

        private void SaveComparisonType(DeviceComparisonTypes type)
        {
            Properties.Settings.Default.DashboardComparisonType = type.ToString();
            Properties.Settings.Default.Save();
        }

        private void UpdateComparisonType(DeviceComparisonTypes comparisonType)
        {
            var data = new EventData(this);
            data.Id = "SORT_DEVICES";
            data.Data01 = comparisonType;

            GetSentData(data);

            SendData?.Invoke(data);
        }

        #endregion

        private void SelectPage(ListButton lb)
        {
            foreach (var olb in Pages)
            {
                if (olb == lb) olb.IsSelected = true;
                else olb.IsSelected = false;
            }

            foreach (var category in SubCategories)
            {
                var config = category.PluginConfigurations.Find(x => GetPluginName(x.Name) == GetPluginName(lb.Text));
                if (config != null)
                {
                    currentPage = config;
                    break;
                }
            }

            // Set Page as PageContent
            PageContent = lb.DataObject as UserControl;

            // Save selected page
            int index = Pages.ToList().FindIndex(x => x == lb);
            if (index >= 0)
            {
                Properties.Settings.Default.DashboardSelectedPage = index;
                Properties.Settings.Default.Save();
            }

            // Load Saved Zoom Level
            ZoomLevel = MainWindow.LoadSavedPageZoomValue(lb.DataObject as IPage);
        }

        private void SelectNextPage()
        {
            if (currentPage != null && Pages.Count > 0)
            {
                int currentIndex = Pages.ToList().FindIndex(x => x.Text == currentPage.Name);
                if (currentIndex >= 0)
                {
                    int next = 0;

                    if (currentIndex < Pages.Count - 1) next = currentIndex + 1;

                    SelectPage(Pages[next]);
                }
            }
        }

        private void SelectPreviousPage()
        {
            if (currentPage != null && Pages.Count > 0)
            {
                int currentIndex = Pages.ToList().FindIndex(x => x.Text == currentPage.Name);
                if (currentIndex >= 0)
                {
                    int next = Pages.Count - 1;

                    if (currentIndex > 0) next = currentIndex - 1;

                    SelectPage(Pages[next]);
                }
            }
        }


        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            int selectedPage = Properties.Settings.Default.DashboardSelectedPage;

            if (Pages.Count > selectedPage)
            {
                ListButton lb = Pages[selectedPage];
                SelectPage(lb);
            }
        }

        private void Expand_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsExpanded = !IsExpanded;

            Properties.Settings.Default.DashboardIsExpanded = IsExpanded;
            Properties.Settings.Default.Save();
        }

        private void SelectDate_Clicked(TrakHound_UI.Button bt)
        {
            DateMenuShown = !DateMenuShown;
        }

        private void ResetStatusTimes_Clicked(TrakHound_UI.Button bt)
        {
            From = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0, DateTimeKind.Local);
            To = From.AddDays(1);

            SaveDashboardTimespan();
            SendDashboardTimespan();
        }

        private void From_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = (ComboBox)sender;
            if (combo.IsKeyboardFocused || combo.IsMouseCaptured)
            {
                SaveDashboardFromTime();
                SendDashboardTimespan();
            }
        }

        private void To_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = (ComboBox)sender;
            if (combo.IsKeyboardFocused || combo.IsMouseCaptured)
            {
                SaveDashboardToTime();
                SendDashboardTimespan();
            }
        }

        private void Sort_Clicked(TrakHound_UI.Button bt)
        {
            var type = DeviceComparisonTypes.DEVICE_ID;
            UpdateComparisonType(type);
            SaveComparisonType(type);
        }

        private void ComparisonType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cmbx = (ComboBox)sender;
            var val = cmbx.SelectedValue;
            if (val != null)
            {
                var type = (DeviceComparisonTypes)val;
                UpdateComparisonType(type);
                SaveComparisonType(type);
            }
        }
    }



}
