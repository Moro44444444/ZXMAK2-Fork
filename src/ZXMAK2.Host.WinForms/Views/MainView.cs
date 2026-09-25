using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Host.Presentation.Interfaces;
using ZXMAK2.Host.WinForms.Mdx;
using ZXMAK2.Host.WinForms.Controls;
using ZXMAK2.Host.WinForms.Tools;
using ZXMAK2.Host.WinForms.Services;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Services;
using ZXMAK2.Resources;
using ZXMAK2.Mvvm;
using ZXMAK2.Mvvm.BindingTools;
using ZXMAK2.Host.WinForms.BindingTools;


namespace ZXMAK2.Host.WinForms.Views
{
    public partial class MainView : Form, IMainView, ICommandManager, INotifyPropertyChanged
    {
        #region Fields

        private readonly IResolver _resolver;
        private readonly BindingService _binding;
        private readonly ToolStripMenuItem _menuToolsQuickBoot = new ToolStripMenuItem();
        private readonly Timer _quickBootStateTimer = new Timer();
        // This is deliberately the only size constant for all three status dots.
        // It can be adjusted after visual feedback without redrawing the toolbar icons.
        private const int MediaStatusDotDiameter = 10;
        private const int MediaToolbarArtworkWidth = 52;
        private const int MediaToolbarArtworkHeight = 36;
        private const int MediaToolbarDropDownWidth = 74;
        private const int MediaToolbarDropDownHeight = 42;
        private readonly Dictionary<MediaStatusKind, ToolStripDropDownButton> _mediaButtons =
            new Dictionary<MediaStatusKind, ToolStripDropDownButton>();
        private readonly Dictionary<MediaStatusKind, MediaState?> _mediaMountedStates =
            new Dictionary<MediaStatusKind, MediaState?>();
        private readonly List<ToolStripItemBindingAdapter> _mediaItemAdapters =
            new List<ToolStripItemBindingAdapter>();
        private bool? _quickBootAvailable;
        private readonly ISuccessCommand[] _openSdCommands =
            new ISuccessCommand[2];
        private readonly ISuccessCommand[] _ejectSdCommands =
            new ISuccessCommand[2];
        private readonly ToolStripMenuItem[] _ejectSdMenuItems =
            new ToolStripMenuItem[2];
        private readonly MediaState?[] _sdMountedStates = new MediaState?[2];
        private ISuccessCommand _openHddCommand;
        private ISuccessCommand _ejectHddCommand;
        private readonly ISuccessCommand[] _openFddCommands = new ISuccessCommand[4];
        private readonly ISuccessCommand[] _ejectFddCommands = new ISuccessCommand[4];
        private readonly ToolStripMenuItem[] _ejectFddMenuItems =
            new ToolStripMenuItem[4];
        private readonly MediaState?[] _fddMountedStates = new MediaState?[4];
        private ISuccessCommand _connectOpticalCommand;
        private ISuccessCommand _ejectOpticalCommand;
        private ToolStripSplitButton _tapeButton;
        private ToolStripSplitButton _opticalButton;
        private ToolStripMenuItem _opticalEjectMenuItem;
        private MediaState? _tapeState;
        private MediaState? _opticalState;
        private readonly ToolStripMenuItem _tapeLoadMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _tapeEjectMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _tapePlayMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _tapeStopMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _tapeRewindMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _tapeQuickLoadMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _tapeAutoPlayMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _tapePlayerMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _opticalSettingsMenuItem = new ToolStripMenuItem("CD-ROM Settings...");

        private IHostService _host;

        //private Size _size;
        private Point _location;
        private FormBorderStyle _style;
        private bool _isToolBarPopupActive;
        private bool _isFullScreenChanging;
        private bool _isFormShown;

        #endregion Fields


        static MainView()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => Logger.Error(e.Exception, "Application.ThreadException");
        }

        public MainView(IResolver resolver)
        {
            _resolver = resolver;
            _binding = new BindingService();
            _binding.RegisterAdapterFactory<Control>(
                arg => new ControlBindingAdapter(arg));
            _binding.RegisterAdapterFactory<ToolStripItem>(
                arg => new ToolStripItemBindingAdapter(arg));

            SetStyle(ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint, true);
            InitializeComponent();
            InitializeMediaToolbar();
            Icon = ResourceImages.IconApp;
            LoadMachineMenu();

            Bind();

            _quickBootStateTimer.Interval = 100;
            _quickBootStateTimer.Tick += QuickBootStateTimer_OnTick;
            _quickBootStateTimer.Start();
            menuTools.DropDownOpening += MenuTools_OnDropDownOpening;
        }


        private void Bind()
        {
            _binding.Bind(this, "IsToolBarEnabled", "IsToolBarEnabled");
            _binding.Bind(this, "IsStatusBarEnabled", "IsStatusBarEnabled");
            _binding.Bind(this, "RenderSize", "RenderSize");
            BindCommandLight(menuViewSizeX1, "CommandViewScaleRatio", 1);
            BindCommandLight(menuViewSizeX2, "CommandViewScaleRatio", 2);
            BindCommandLight(menuViewSizeX3, "CommandViewScaleRatio", 3);
            BindCommandLight(menuViewSizeX4, "CommandViewScaleRatio", 4);
            _binding.Bind(this, "RenderScaleRatio", "RenderScaleRatio");

            BindCommand(menuFileOpen, "CommandFileOpen", this);
            BindCommand(menuFileSaveAs, "CommandFileSave", this);
            BindCommand(menuFileExit, "CommandFileExit");
            BindCommand(menuViewFullScreen, "CommandViewFullScreen");
            BindCommand(menuVmPause, "CommandVmPause");
            BindCommand(menuVmMaximumSpeed, "CommandVmMaxSpeed");
            BindCommand(menuVmWarmReset, "CommandVmWarmReset");
            BindCommand(menuVmColdReset, "CommandVmColdReset");
            BindCommand(menuVmNmi, "CommandVmNmi");
            BindCommand(menuVmSettings, "CommandVmSettings", this);
            BindCommand(menuHelpViewHelp, "CommandHelpViewHelp", this);
            BindCommand(menuHelpKeyboardHelp, "CommandHelpKeyboardHelp", this);
            BindCommand(menuHelpAbout, "CommandHelpAbout", this);

            BindCommand(tbrButtonOpen, "CommandFileOpen", this);
            BindCommand(tbrButtonSave, "CommandFileSave", this);
            BindCommand(tbrButtonPause, "CommandVmPause");
            BindCommand(tbrButtonMaxSpeed, "CommandVmMaxSpeed");
            BindCommand(tbrButtonWarmReset, "CommandVmWarmReset");
            BindCommand(tbrButtonColdReset, "CommandVmColdReset");
            BindCommand(tbrButtonFullScreen, "CommandViewFullScreen");
            BindCommand(tbrButtonQuickLoad, "CommandQuickLoad");
            BindCommand(_menuToolsQuickBoot, "CommandQuickLoad");
            BindCommand(tbrButtonSettings, "CommandVmSettings", this);
            BindCommand(_tapeLoadMenuItem, "CommandTapeLoad");
            BindCommand(_tapeEjectMenuItem, "CommandTapeEject");
            BindCommand(_tapePlayMenuItem, "CommandTapePlay");
            BindCommand(_tapeStopMenuItem, "CommandTapeStop");
            BindCommand(_tapeRewindMenuItem, "CommandTapeRewind");
            BindCommand(_tapeQuickLoadMenuItem, "CommandTapeQuickLoad");
            BindCommand(_tapeAutoPlayMenuItem, "CommandTapeAutoPlay");
            BindCommand(_tapePlayerMenuItem, "CommandTapeOpenPlayer");
            BindCommand(
                _opticalSettingsMenuItem,
                "CommandVmSettings",
                "IDE PentEvo");

            BindCommand(menuViewCustomizeShowToolBar, "CommandViewToolBar");
            BindCommand(menuViewCustomizeShowStatusBar, "CommandViewStatusBar");
            _binding.Bind(this, "IsToolBarEnabled", "CommandViewToolBar.Checked");
            _binding.Bind(this, "IsStatusBarEnabled", "CommandViewStatusBar.Checked");

            BindCommandLight(menuViewFrameSyncTime, "CommandViewSyncSource", SyncSource.Time);
            BindCommandLight(menuViewFrameSyncSound, "CommandViewSyncSource", SyncSource.Sound);
            BindCommandLight(menuViewFrameSyncVideo, "CommandViewSyncSource", SyncSource.Video);
            _binding.Bind(this, "SelectedSyncSource", "SyncSource");

            BindCommandLight(menuViewScaleModeStretch, "CommandViewScaleMode", ScaleMode.Stretch);
            BindCommandLight(menuViewScaleModeKeepProportion, "CommandViewScaleMode", ScaleMode.KeepProportion);
            BindCommandLight(menuViewScaleModeFixedPixelSize, "CommandViewScaleMode", ScaleMode.FixedPixelSize);
            BindCommandLight(menuViewScaleModeSquarePixelSize, "CommandViewScaleMode", ScaleMode.SquarePixelSize);
            BindCommand(menuViewScaleModeNoBorder, "CommandViewNoBorder");
            _binding.Bind(this, "SelectedScaleMode", "RenderScaleMode");
            _binding.Bind(renderVideo, "NoBorder", "RenderNoBorder");
            _binding.Bind(menuViewScaleModeNoBorder, "Checked", "RenderNoBorder");

            BindCommandLight(menuViewVideoFilterNone, "CommandViewVideoFilter", VideoFilter.None);
            BindCommandLight(menuViewVideoFilterNoFlick, "CommandViewVideoFilter", VideoFilter.NoFlick);
            _binding.Bind(this, "SelectedVideoFilter", "RenderVideoFilter");

            BindCommand(menuViewSmoothing, "CommandViewSmooth");
            BindCommand(menuViewMimicTv, "CommandViewMimicTv");
            BindCommand(menuViewDisplayIcon, "CommandViewDisplayIcon");
            BindCommand(menuViewDebugInfo, "CommandViewDebugInfo");

            _binding.Bind(renderVideo, "AntiAlias", "CommandViewSmooth.Checked");
            _binding.Bind(renderVideo, "MimicTv", "CommandViewMimicTv.Checked");
            _binding.Bind(renderVideo, "DisplayIcon", "CommandViewDisplayIcon.Checked");
            _binding.Bind(renderVideo, "DebugInfo", "CommandViewDebugInfo.Checked");

            _binding.Bind(this, "Title", "Title");
            _binding.Bind(this, "IsFullScreen", "IsFullScreen");
            _binding.Bind(this, "IsRunning", "IsRunning");
            _binding.Bind(renderVideo, "IsRunning", "IsRunning");

            var imagePause = global::ZXMAK2.Host.WinForms.Properties.Resources.EmuPause_32x32;
            var imageResume = global::ZXMAK2.Host.WinForms.Properties.Resources.EmuResume_32x32;
            var imageWindowed = global::ZXMAK2.Host.WinForms.Properties.Resources.EmuWindowed_32x32;
            var imageFullScreen = global::ZXMAK2.Host.WinForms.Properties.Resources.EmuFullScreen_32x32;
            _binding.Bind(
                tbrButtonPause,
                "Image",
                "IsRunning",
                arg => (bool)arg ? imagePause : imageResume);
            _binding.Bind(
                tbrButtonFullScreen,
                "Image",
                "IsFullScreen",
                arg => (bool)arg ? imageWindowed : imageFullScreen);

            _binding.Bind(this, "CommandViewFullScreen", "CommandViewFullScreen");
            _binding.Bind(this, "CommandVmPause", "CommandVmPause");
            _binding.Bind(this, "CommandVmMaxSpeed", "CommandVmMaxSpeed");
            _binding.Bind(this, "CommandVmWarmReset", "CommandVmWarmReset");
            _binding.Bind(this, "CommandTapePause", "CommandTapePause");
            _binding.Bind(this, "CommandTapeOpenPlayer", "CommandTapeOpenPlayer");
            _binding.Bind(this, "CommandQuickLoad", "CommandQuickLoad");
            _binding.Bind(this, "CommandOpenUri", "CommandOpenUri");
            _binding.Bind(this, "CommandMachineSwitch", "CommandMachineSwitch");
        }


        #region Commands

        public ICommand CommandViewFullScreen { get; set; }
        public ICommand CommandViewNoBorder { get; set; }
        public ICommand CommandVmPause { get; set; }
        public ICommand CommandVmMaxSpeed { get; set; }
        public ICommand CommandVmWarmReset { get; set; }
        public ICommand CommandTapePause { get; set; }
        public ICommand CommandTapeOpenPlayer { get; set; }
        public ICommand CommandQuickLoad { get; set; }
        public ICommand CommandOpenUri { get; set; }
        public ICommand CommandMachineSwitch { get; set; }

        #endregion Commands


        #region IMainView

        public object DataContext
        {
            get { return _binding.DataContext; }
            set { _binding.DataContext = value; }
        }

        private string _title;
        
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                UpdateTitle();
            }
        }

        private bool _isRunning;

        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                _isRunning = value;
                UpdateTitle();
            }
        }

        public IHostService Host
        {
            get { return _host; }
        }

        public ICommandManager CommandManager
        {
            get { return this; }
        }

        public event EventHandler ViewOpened;
        public event EventHandler ViewClosed;
        public event EventHandler RequestFrame;

        public void Run()
        {
            Application.Run(this);
        }

        #endregion IMainView


        #region IHostUi

        private List<ToolStripItemBindingAdapter> _deviceItemAdapters = new List<ToolStripItemBindingAdapter>();

        public void Clear()
        {
            _deviceItemAdapters
                .ForEach(arg => arg.Dispose());
            _deviceItemAdapters.Clear();
            menuTools.DropDownItems.Clear();
            Array.Clear(_openSdCommands, 0, _openSdCommands.Length);
            Array.Clear(_ejectSdCommands, 0, _ejectSdCommands.Length);
            _openHddCommand = null;
            _ejectHddCommand = null;
            Array.Clear(_openFddCommands, 0, _openFddCommands.Length);
            Array.Clear(_ejectFddCommands, 0, _ejectFddCommands.Length);
            _connectOpticalCommand = null;
            _ejectOpticalCommand = null;
            _mediaMountedStates.Clear();
            ClearMediaToolbarMenus();
            UpdateMediaToolbarStates();
            _quickBootAvailable = null;
        }

        public void Add(ICommand command)
        {
            if (RegisterMediaCommand(command))
            {
                RebuildMediaToolbarMenus();
                return;
            }
            var subMenu = menuTools.DropDownItems.Add(command.Text) as ToolStripMenuItem;
            if (subMenu == null)
            {
                return;
            }
            var adapter = new ToolStripItemBindingAdapter(subMenu);
            adapter.CommandParameter = this;
            adapter.Command = command;
            _deviceItemAdapters.Add(adapter);
            SortMenuTools();
        }

        private bool RegisterMediaCommand(ICommand command)
        {
            var mediaCommand = command as IMediaCommand;
            if (mediaCommand == null)
            {
                return false;
            }
            switch (mediaCommand.MediaKind)
            {
                case MediaCommandKind.SecureDigital:
                    if (mediaCommand.DriveIndex < 0 ||
                        mediaCommand.DriveIndex >= _openSdCommands.Length)
                    {
                        return false;
                    }
                    if (mediaCommand.MediaAction == MediaCommandAction.Load)
                    {
                        _openSdCommands[mediaCommand.DriveIndex] = mediaCommand;
                    }
                    else
                    {
                        _ejectSdCommands[mediaCommand.DriveIndex] = mediaCommand;
                    }
                    return true;
                case MediaCommandKind.HardDisk:
                    if (mediaCommand.MediaAction == MediaCommandAction.Load)
                    {
                        _openHddCommand = mediaCommand;
                    }
                    else
                    {
                        _ejectHddCommand = mediaCommand;
                    }
                    return true;
                case MediaCommandKind.Floppy:
                    if (mediaCommand.DriveIndex < 0 ||
                        mediaCommand.DriveIndex >= _openFddCommands.Length)
                    {
                        return false;
                    }
                    if (mediaCommand.MediaAction == MediaCommandAction.Load)
                    {
                        _openFddCommands[mediaCommand.DriveIndex] = mediaCommand;
                    }
                    else
                    {
                        _ejectFddCommands[mediaCommand.DriveIndex] = mediaCommand;
                    }
                    return true;
                case MediaCommandKind.OpticalDisc:
                    if (mediaCommand.MediaAction == MediaCommandAction.Load)
                    {
                        _connectOpticalCommand = mediaCommand;
                    }
                    else
                    {
                        _ejectOpticalCommand = mediaCommand;
                    }
                    return true;
            }
            return false;
        }

        private void InitializeMediaToolbar()
        {
            // Keep every toolbar drop-down equally wide: its artwork must not compete
            // with the arrow that opens the corresponding menu.
            tbrDropDownMachines.AutoSize = false;
            tbrDropDownMachines.ImageScaling = ToolStripItemImageScaling.None;
            tbrDropDownMachines.Size = new Size(
                MediaToolbarDropDownWidth,
                MediaToolbarDropDownHeight);
            var index = tbrStrip.Items.IndexOf(tbrButtonSdImage);
            tbrStrip.Items.Remove(tbrButtonSdImage);
            tbrButtonSdImage.Dispose();
            _tapeButton = AddMediaSplitButton(
                MediaStatusKind.Tape,
                "Tape",
                index++);
            _tapeButton.ButtonClick += TapeButton_OnClick;
            InitializeTapeMenu();
            AddMediaToolbarButton(MediaStatusKind.Floppy, "Floppy disk images", index++);
            AddMediaToolbarButton(MediaStatusKind.HardDisk, "HDD image", index++);
            AddMediaToolbarButton(MediaStatusKind.SecureDigital, "SD card image", index++);
            _opticalButton = AddMediaSplitButton(
                MediaStatusKind.OpticalDisc,
                "CD-ROM / DVD-ROM",
                index);
            _opticalButton.ButtonClick += OpticalButton_OnClick;
        }

        private ToolStripSplitButton AddMediaSplitButton(
            MediaStatusKind mediaKind,
            string toolTip,
            int index)
        {
            var button = new ToolStripSplitButton();
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
            button.ImageTransparentColor = Color.Magenta;
            button.ImageScaling = ToolStripItemImageScaling.None;
            button.AutoSize = false;
            button.Size = new Size(
                MediaToolbarDropDownWidth,
                MediaToolbarDropDownHeight);
            button.Text = toolTip;
            button.ToolTipText = toolTip;
            button.Enabled = false;
            tbrStrip.Items.Insert(index, button);
            SetSplitButtonImage(button, mediaKind, MediaState.Unavailable);
            return button;
        }

        private void InitializeTapeMenu()
        {
            _tapeButton.DropDownItems.AddRange(new ToolStripItem[]
            {
                _tapeLoadMenuItem,
                _tapeEjectMenuItem,
                new ToolStripSeparator(),
                _tapePlayMenuItem,
                _tapeStopMenuItem,
                _tapeRewindMenuItem,
                new ToolStripSeparator(),
                _tapeQuickLoadMenuItem,
                _tapeAutoPlayMenuItem,
                new ToolStripSeparator(),
                _tapePlayerMenuItem,
            });
        }

        private void TapeButton_OnClick(object sender, EventArgs e)
        {
            OnCommand(CommandTapeOpenPlayer);
        }

        private void OpticalButton_OnClick(object sender, EventArgs e)
        {
            if (_connectOpticalCommand != null &&
                _connectOpticalCommand.CanExecute(this))
            {
                ExecuteMediaCommand(_connectOpticalCommand, this, true);
            }
        }

        private void AddMediaToolbarButton(MediaStatusKind mediaKind, string toolTip, int index)
        {
            var button = new ToolStripDropDownButton();
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
            button.ImageTransparentColor = Color.Magenta;
            button.ImageScaling = ToolStripItemImageScaling.None;
            button.AutoSize = false;
            button.Size = new Size(
                MediaToolbarDropDownWidth,
                MediaToolbarDropDownHeight);
            button.Text = toolTip;
            button.ToolTipText = toolTip;
            button.Enabled = false;
            _mediaButtons.Add(mediaKind, button);
            tbrStrip.Items.Insert(index, button);
            SetMediaToolbarImage(mediaKind, MediaState.Unavailable);
        }

        private void ClearMediaToolbarMenus()
        {
            _mediaItemAdapters.ForEach(adapter => adapter.Dispose());
            _mediaItemAdapters.Clear();
            foreach (var item in _ejectFddMenuItems)
            {
                if (item != null && item.Image != null)
                {
                    item.Image.Dispose();
                    item.Image = null;
                }
            }
            foreach (var item in _ejectSdMenuItems)
            {
                if (item != null && item.Image != null)
                {
                    item.Image.Dispose();
                    item.Image = null;
                }
            }
            foreach (var button in _mediaButtons.Values)
            {
                button.DropDownItems.Clear();
                button.Enabled = false;
            }
            Array.Clear(_ejectFddMenuItems, 0, _ejectFddMenuItems.Length);
            Array.Clear(_fddMountedStates, 0, _fddMountedStates.Length);
            Array.Clear(_ejectSdMenuItems, 0, _ejectSdMenuItems.Length);
            Array.Clear(_sdMountedStates, 0, _sdMountedStates.Length);
            if (_opticalEjectMenuItem != null && _opticalEjectMenuItem.Image != null)
            {
                _opticalEjectMenuItem.Image.Dispose();
                _opticalEjectMenuItem.Image = null;
            }
            if (_opticalButton != null)
            {
                _opticalButton.DropDownItems.Clear();
                _opticalButton.Enabled = false;
            }
            _opticalEjectMenuItem = null;
            _opticalState = null;
        }

        private void RebuildMediaToolbarMenus()
        {
            ClearMediaToolbarMenus();
            var sdNames = new[] { "Z-controller", "NeoGS" };
            for (var sd = 0; sd < sdNames.Length; sd++)
            {
                AddMediaMenuItem(
                    MediaStatusKind.SecureDigital,
                    "Load " + sdNames[sd],
                    _openSdCommands[sd],
                    true);
                _ejectSdMenuItems[sd] = AddMediaMenuItem(
                    MediaStatusKind.SecureDigital,
                    "Eject " + sdNames[sd],
                    _ejectSdCommands[sd],
                    true);
            }
            AddMediaMenuItem(MediaStatusKind.HardDisk, "Load HDD", _openHddCommand, true);
            AddMediaMenuItem(MediaStatusKind.HardDisk, "Eject HDD", _ejectHddCommand, true);
            for (var drive = 0; drive < 4; drive++)
            {
                AddMediaMenuItem(
                    MediaStatusKind.Floppy,
                    string.Format("Load {0}:", (char)('A' + drive)),
                    _openFddCommands[drive],
                    false);
                _ejectFddMenuItems[drive] = AddMediaMenuItem(
                    MediaStatusKind.Floppy,
                    string.Format("Eject {0}:", (char)('A' + drive)),
                    _ejectFddCommands[drive],
                    false);
            }
            foreach (var pair in _mediaButtons)
            {
                pair.Value.Enabled = pair.Value.DropDownItems.Count > 0;
            }
            AddMediaMenuItem(
                _opticalButton.DropDownItems,
                "Connect CD-ROM / DVD-ROM...",
                _connectOpticalCommand,
                true);
            _opticalEjectMenuItem = AddMediaMenuItem(
                _opticalButton.DropDownItems,
                "Eject / Disconnect CD-ROM",
                _ejectOpticalCommand,
                true);
            _opticalButton.DropDownItems.Add(new ToolStripSeparator());
            _opticalButton.DropDownItems.Add(_opticalSettingsMenuItem);
            UpdateMediaToolbarStates();
        }

        private ToolStripMenuItem AddMediaMenuItem(
            MediaStatusKind mediaKind,
            string text,
            ISuccessCommand sourceCommand,
            bool requiresPowerCycle)
        {
            return AddMediaMenuItem(
                _mediaButtons[mediaKind].DropDownItems,
                text,
                sourceCommand,
                requiresPowerCycle);
        }

        private ToolStripMenuItem AddMediaMenuItem(
            ToolStripItemCollection target,
            string text,
            ISuccessCommand sourceCommand,
            bool requiresPowerCycle)
        {
            var item = new ToolStripMenuItem(text);
            if (sourceCommand == null)
            {
                item.Enabled = false;
                target.Add(item);
                return item;
            }
            var command = new CommandDelegate(
                arg => ExecuteMediaCommand(sourceCommand, arg, requiresPowerCycle),
                arg => sourceCommand.CanExecute(arg),
                text);
            var adapter = new ToolStripItemBindingAdapter(item);
            adapter.CommandParameter = this;
            adapter.Command = command;
            _mediaItemAdapters.Add(adapter);
            target.Add(item);
            return item;
        }

        private void ExecuteMediaCommand(
            ISuccessCommand command,
            object commandParameter,
            bool requiresPowerCycle)
        {
            var viewModel = DataContext as IMainViewModel;
            if (viewModel == null)
            {
                command.Execute(commandParameter);
                return;
            }
            if (requiresPowerCycle)
            {
                viewModel.ExecuteMediaChange(command, commandParameter);
            }
            else
            {
                viewModel.ExecuteFloppyMediaChange(command, commandParameter);
            }
        }

        #endregion IHostUi


        #region IMainView Events

        private void OnViewOpened()
        {
            var handler = ViewOpened;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnViewClosed()
        {
            var handler = ViewClosed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnRequestFrame()
        {
            var handler = RequestFrame;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        #endregion IMainView Events


        #region Form Event Handlers

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                NativeMethods.TimeBeginPeriod(1);
                renderVideo.InitWnd();
                _host = CreateHost();
                OnViewOpened();
                //_host.MediaRecorder = new ZXMAK2.Host.Media.MediaRecorder("C:\\test.mp4", 320*2, 256*2, _host.SampleRate);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private IHostService CreateHost()
        {
            var viewResolver = Locator.TryResolve<IResolver>("View");
            if (viewResolver == null)
            {
                return new HostService(renderVideo, null, null, null, null);
            }
            var arg = new Argument("form", this);
            var sound = viewResolver.TryResolve<IHostSound>(arg);
            var keyboard = viewResolver.TryResolve<IHostKeyboard>(arg);
            var mouse = viewResolver.TryResolve<IHostMouse>(arg);
            var joystick = viewResolver.TryResolve<IHostJoystick>(arg);
            return new HostService(renderVideo, sound, keyboard, mouse, joystick);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _isFormShown = true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            _isFormShown = false;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            try
            {
                _quickBootStateTimer.Stop();
                _quickBootStateTimer.Tick -= QuickBootStateTimer_OnTick;
                _quickBootStateTimer.Dispose();
                menuTools.DropDownOpening -= MenuTools_OnDropDownOpening;
                _mediaItemAdapters.ForEach(adapter => adapter.Dispose());
                _mediaItemAdapters.Clear();
                foreach (var button in _mediaButtons.Values)
                {
                    if (button.Image != null)
                    {
                        button.Image.Dispose();
                        button.Image = null;
                    }
                }
                if (_tapeButton != null && _tapeButton.Image != null)
                {
                    _tapeButton.Image.Dispose();
                    _tapeButton.Image = null;
                }
                if (_opticalButton != null && _opticalButton.Image != null)
                {
                    _opticalButton.Image.Dispose();
                    _opticalButton.Image = null;
                }
                if (_tapeEjectMenuItem.Image != null)
                {
                    _tapeEjectMenuItem.Image.Dispose();
                    _tapeEjectMenuItem.Image = null;
                }
                if (_opticalEjectMenuItem != null &&
                    _opticalEjectMenuItem.Image != null)
                {
                    _opticalEjectMenuItem.Image.Dispose();
                    _opticalEjectMenuItem.Image = null;
                }
                OnViewClosed();
                if (_host != null)
                {
                    _host.Dispose();
                    _host = null;
                }
                renderVideo.FreeWnd();
                NativeMethods.TimeEndPeriod(1);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                _resolver.Resolve<IUserMessage>().ErrorDetails(ex);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout(true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_host.IsCaptured)
            {
                e.SuppressKeyPress = true;
            }
            if (e.Alt && e.Control)
            {
                _host.Uncapture();
            }
            // FULLSCREEN
            if (e.Alt && e.KeyCode == Keys.Enter)
            {
                if (e.Alt)
                {
                    OnCommand(CommandViewFullScreen);
                }
                e.Handled = true;
                return;
            }
            //RESET
            if (e.KeyCode == Keys.F12)
            {
                if (e.Alt && e.Control)
                {
                    RequestFactoryReset();
                }
                else if (e.Control && !e.Alt)
                {
                    RequestCmosReset();
                }
                else if (!e.Alt && !e.Shift)
                {
                    // This form does not receive a debugger-window F12 key,
                    // so the debugger keeps its own existing F12 behavior.
                    OnCommand(CommandVmWarmReset);
                    RefreshMediaToolbarStates();
                }
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }
            if (e.Alt && e.Control &&
                (e.KeyCode == Keys.Insert || e.KeyCode == Keys.End))
            {
                OnCommand(CommandVmWarmReset, true);
                e.Handled = true;
                return;
            }
            // STOP/RUN
            if (e.KeyCode == Keys.Pause)
            {
                OnCommand(CommandVmPause);
                e.Handled = true;
                return;
            }
            // Max Speed
            if (e.Alt && e.KeyCode == Keys.Scroll)
            {
                OnCommand(CommandVmMaxSpeed);
                e.Handled = true;
                return;
            }
            if (e.Alt && e.Control && e.KeyCode == Keys.F1)
            {
                OnCommand(CommandQuickLoad);
                e.Handled = true;
                return;
            }
            if (e.Alt && e.Control && e.KeyCode == Keys.F8)
            {
                OnCommand(CommandTapePause);
                e.Handled = true;
                return;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            //RESET
            if (e.Alt && e.Control &&
                (e.KeyCode == Keys.Insert || e.KeyCode == Keys.End))
            {
                OnCommand(CommandVmWarmReset, false);
                e.Handled = true;
                return;
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!Created)
            {
                return;
            }
            OnCommand(CommandVmWarmReset, false);
        }


        #endregion Form Event Handlers


        #region Drag-n-Drop

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            try
            {
                if (!CanFocus)
                {
                    e.Effect = DragDropEffects.None;
                    return;
                }
                var ddw = new DragDataWrapper(e.Data);
                var allowOpen = false;
                if (ddw.IsFileDrop)
                {
                    var uri = new Uri(Path.GetFullPath(ddw.GetFilePath()));
                    allowOpen = CanExecute(CommandOpenUri, uri);
                }
                else if (ddw.IsLinkDrop)
                {
                    var uri = new Uri(ddw.GetLinkUri());
                    allowOpen = CanExecute(CommandOpenUri, uri);
                }
                e.Effect = allowOpen ? DragDropEffects.Link : DragDropEffects.None;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        protected override void OnDragDrop(DragEventArgs e)
        {
            base.OnDragDrop(e);
            try
            {
                if (!CanFocus)
                {
                    return;
                }
                var ddw = new DragDataWrapper(e.Data);
                if (ddw.IsFileDrop)
                {
                    var uri = new Uri(Path.GetFullPath(ddw.GetFilePath()));
                    this.Activate();
                    this.BeginInvoke(new Action(() => OnCommand(CommandOpenUri, uri)));

                    //string fileName = ddw.GetFilePath();
                    //if (fileName != string.Empty)
                    //{
                    //    this.Activate();
                    //    this.BeginInvoke(new OpenFileHandler(OpenFile), fileName, true);
                    //}
                }
                else if (ddw.IsLinkDrop)
                {
                    var uri = new Uri(ddw.GetLinkUri());
                    this.Activate();
                    this.BeginInvoke(new Action(() => OnCommand(CommandOpenUri, uri)));

                    //string linkUrl = ddw.GetLinkUri();
                    //if (linkUrl != string.Empty)
                    //{
                    //    Uri fileUri = new Uri(linkUrl);
                    //    this.Activate();
                    //    this.BeginInvoke(new OpenUriHandler(OpenUri), fileUri);
                    //}
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        #endregion Drag-n-Drop


        #region Layout

        private bool _isToolBarEnabled;

        public bool IsToolBarEnabled
        {
            get { return _isToolBarEnabled; }
            set
            {
                _isToolBarEnabled = value;
                Relayout(false);
            }
        }

        private bool _isStatusBarEnabled;

        public bool IsStatusBarEnabled
        {
            get { return _isStatusBarEnabled; }
            set
            {
                _isStatusBarEnabled = value;
                Relayout(false);
            }
        }

        private bool _renderSizeInitialized;

        private Size _renderSize;

        public Size RenderSize
        {
            get { return _renderSize; }
            set
            {
                if (_renderSize == value && _renderSizeInitialized)
                {
                    return;
                }
                _renderSizeInitialized = true;
                if (value.Width < 0)
                    value = new Size(0, value.Height);
                if (value.Height < 0)
                    value = new Size(value.Width, 0);
                _renderSize = value;
                Relayout(false);
            }
        }

        private int? _renderScaleRatio;

        public int? RenderScaleRatio
        {
            get { return _renderScaleRatio; }
            set
            {
                menuViewSizeX1.Checked = value.HasValue && value.Value == 1;
                menuViewSizeX2.Checked = value.HasValue && value.Value == 2;
                menuViewSizeX3.Checked = value.HasValue && value.Value == 3;
                menuViewSizeX4.Checked = value.HasValue && value.Value == 4;
                _renderScaleRatio = value;
            }
        }

        private void Relayout(bool srcUserResizing)
        {
            if (!_renderSizeInitialized || _isFullScreenChanging)
            {
                return;
            }
            if (IsFullScreen)
            {
                var menuEnabled = _isToolBarPopupActive;
                var toolEnabled = IsToolBarEnabled && _isToolBarPopupActive;
                var statEnabled = IsStatusBarEnabled;

                sbrStrip.SizingGrip = false;
                mnuStrip.Visible = menuEnabled;
                tbrStrip.Visible = toolEnabled;
                sbrStrip.Visible = statEnabled;

                var sbarHeight = statEnabled ? sbrStrip.Height : 0;
                var size = new Size(ClientSize.Width, ClientSize.Height - sbarHeight);
                var pos = new Point(0, 0);
                if (renderVideo.Location != pos)
                {
                    renderVideo.Location = pos;
                }
                if (renderVideo.Size != size)
                {
                    renderVideo.Size = size;
                }
            }
            else
            {
                var menuEnabled = true;
                var toolEnabled = IsToolBarEnabled;
                var statEnabled = IsStatusBarEnabled;

                sbrStrip.SizingGrip = true;
                mnuStrip.Visible = menuEnabled;
                tbrStrip.Visible = toolEnabled;
                sbrStrip.Visible = statEnabled;

                var menuHeight = menuEnabled ? mnuStrip.Height : 0;
                var tbarHeight = toolEnabled ? tbrStrip.Height : 0;
                var sbarHeight = statEnabled ? sbrStrip.Height : 0;
                var shift = menuHeight + tbarHeight + sbarHeight;
                var notifyNeeded = false;
                if (srcUserResizing)
                {
                    var size = new Size(ClientSize.Width, ClientSize.Height - shift);
                    if (_renderSize != size && _isFormShown)
                    {
                        _renderSize = size;
                        notifyNeeded = true;
                    }
                }
                else
                {
                    var size = new Size(RenderSize.Width, RenderSize.Height + shift);
                    if (ClientSize != size)
                    {
                        ClientSize = size;
                    }
                }
                var pos = new Point(0, menuHeight+tbarHeight);
                if (renderVideo.Location != pos)
                {
                    renderVideo.Location = pos;
                }
                if (renderVideo.Size != RenderSize)
                {
                    renderVideo.Size = RenderSize;
                }
                if (notifyNeeded)
                {
                    OnPropertyChanged("RenderSize");
                }
            }
        }

        private void ScanPopup()
        {
            if (!IsFullScreen)
            {
                _isToolBarPopupActive = false;
                return;
            }
            var pos = PointToClient(Cursor.Position);
            var menuHeight = mnuStrip.Height;
            var tbarHeight = IsToolBarEnabled ? tbrStrip.Height : 0;
            var sbarHeight = IsStatusBarEnabled ? sbrStrip.Height : 0;
            var toolHeight = menuHeight + tbarHeight;
            var isTopArea = pos.X >= 0 && pos.X < ClientSize.Width &&
                pos.Y >= 0 && pos.Y < toolHeight + 2;
            if (_isToolBarPopupActive == isTopArea)
            {
                return;
            }
            _isToolBarPopupActive = isTopArea;
            Relayout(true);
        }

        private bool _isFullScreen;

        public bool IsFullScreen
        {
            get { return _isFullScreen; }
            set
            {
                if (value == _isFullScreen)
                {
                    return;
                }
                _isFullScreen = value;
                if (_isFullScreen)
                {
                    _style = FormBorderStyle;
                    _location = Location;
                    //_size = ClientSize;

                    _isFullScreenChanging = true;
                    FormBorderStyle = FormBorderStyle.None;
                    var screen = Screen.FromControl(this);
                    Location = screen.Bounds.Location;
                    _isFullScreenChanging = false;
                    Size = screen.Bounds.Size;

                    //_host.StartInputCapture();

                    Focus();
                }
                else
                {
                    _isFullScreenChanging = true;
                    Location = _location;
                    FormBorderStyle = _style;
                    _isFullScreenChanging = false;
                    Relayout(false);
                    //ClientSize = _size;

                    _host.Uncapture();
                }
            }
        }

        private void renderVideo_MouseMove(object sender, MouseEventArgs e)
        {
            ScanPopup();
        }

        private void renderVideo_DoubleClick(object sender, EventArgs e)
        {
            if (!renderVideo.Focused)
            {
                return;
            }
            _host.Capture();
        }

        private void renderVideo_Resize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Maximized)
            {
                return;
            }
            WindowState = FormWindowState.Normal;
            OnCommand(CommandViewFullScreen, true);
        }

        private void UpdateTitle()
        {
            var tail = IsRunning ? "ZXMAK2" : "ZXMAK2 [paused]";
            Text = string.IsNullOrEmpty(_title) ?
                tail :
                string.Format("[{0}] - {1}", Title, tail);
        }

        #endregion Layout


        #region View Settings

        private SyncSource _selectedSyncSource;
        
        public SyncSource SelectedSyncSource
        {
            get { return _selectedSyncSource; }
            set
            {
                menuViewFrameSyncTime.Checked = value == SyncSource.Time;
                menuViewFrameSyncSound.Checked = value == SyncSource.Sound;
                menuViewFrameSyncVideo.Checked = value == SyncSource.Video;
                _selectedSyncSource = value;
            }
        }

        private ScaleMode _selectedScaleMode;
        
        public ScaleMode SelectedScaleMode
        {
            get { return _selectedScaleMode; }
            set
            {
                menuViewScaleModeStretch.Checked = value == ScaleMode.Stretch;
                menuViewScaleModeKeepProportion.Checked = value == ScaleMode.KeepProportion;
                menuViewScaleModeFixedPixelSize.Checked = value == ScaleMode.FixedPixelSize;
                menuViewScaleModeSquarePixelSize.Checked = value == ScaleMode.SquarePixelSize;
                renderVideo.ScaleMode = value;
                _selectedScaleMode = value;
            }
        }

        private VideoFilter _selectedVideoFilter;

        public VideoFilter SelectedVideoFilter
        {
            get { return _selectedVideoFilter; }
            set
            {
                menuViewVideoFilterNoFlick.Checked = value == VideoFilter.NoFlick;
                menuViewVideoFilterNone.Checked = value == VideoFilter.None;
                renderVideo.VideoFilter = value;
                _selectedVideoFilter = value;
            }
        }

        #endregion View Settings


        #region Menu Handlers

        private void LoadMachineMenu()
        {
            try
            {
                var machines = new MachinesConfig();
                machines.Load();
                var names = new List<string>(machines.GetNames());
                names.Sort(StringComparer.CurrentCultureIgnoreCase);
                foreach (var name in names)
                {
                    var item = new ToolStripMenuItem(name);
                    item.Tag = name;
                    item.Click += MachineMenuItem_OnClick;
                    tbrDropDownMachines.DropDownItems.Add(item);
                }
                tbrDropDownMachines.Enabled = tbrDropDownMachines.DropDownItems.Count > 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                tbrDropDownMachines.Enabled = false;
            }
        }

        private void MachineMenuItem_OnClick(object sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            var machineName = item == null ? null : item.Tag as string;
            if (string.IsNullOrEmpty(machineName) || !CanExecute(CommandMachineSwitch, machineName))
            {
                return;
            }
            if (ConfirmMachineSwitch(machineName))
            {
                OnCommand(CommandMachineSwitch, machineName);
            }
        }

        private bool ConfirmMachineSwitch(string machineName)
        {
            var dialog = new Form();
            try
            {
                dialog.Text = "Switch Machine";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ClientSize = new Size(340, 104);
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ShowInTaskbar = false;

                var prompt = new Label();
                prompt.AutoSize = true;
                prompt.Location = new Point(16, 18);
                prompt.Text = string.Format("Switch to \"{0}\"?", machineName);

                var buttonSwitch = new Button();
                buttonSwitch.DialogResult = DialogResult.OK;
                buttonSwitch.Location = new Point(168, 62);
                buttonSwitch.Size = new Size(75, 26);
                buttonSwitch.Text = "Switch";
                buttonSwitch.UseVisualStyleBackColor = true;

                var buttonCancel = new Button();
                buttonCancel.DialogResult = DialogResult.Cancel;
                buttonCancel.Location = new Point(249, 62);
                buttonCancel.Size = new Size(75, 26);
                buttonCancel.Text = "Cancel";
                buttonCancel.UseVisualStyleBackColor = true;

                dialog.Controls.Add(prompt);
                dialog.Controls.Add(buttonSwitch);
                dialog.Controls.Add(buttonCancel);
                dialog.AcceptButton = buttonSwitch;
                dialog.CancelButton = buttonCancel;
                return dialog.ShowDialog(this) == DialogResult.OK;
            }
            finally
            {
                dialog.Dispose();
            }
        }

        private void SortMenuTools()
        {
            var list = new List<ToolStripItem>();
            foreach (ToolStripItem item in menuTools.DropDownItems)
            {
                list.Add(item);
            }
            list.Sort(SortMenuToolsComparison);
            menuTools.DropDownItems.Clear();
            foreach (var item in list)
            {
                menuTools.DropDownItems.Add(item);
            }
        }

        private static int SortMenuToolsComparison(ToolStripItem x, ToolStripItem y)
        {
            if (x.Text == y.Text) return 0;
            if (string.Compare(x.Text, "Debugger", true) == 0) return -1;
            if (string.Compare(y.Text, "Debugger", true) == 0) return 1;
            return StringComparer.CurrentCultureIgnoreCase.Compare(x.Text, y.Text);
        }

        private void UpdateMediaToolbarStates()
        {
            var viewModel = DataContext as IMainViewModel;
            foreach (var mediaKind in _mediaButtons.Keys)
            {
                var state = GetAggregateMediaState(viewModel, mediaKind);
                MediaState? lastState;
                if (!_mediaMountedStates.TryGetValue(mediaKind, out lastState) ||
                    !lastState.HasValue || lastState.Value != state)
                {
                    _mediaMountedStates[mediaKind] = state;
                    SetMediaToolbarImage(mediaKind, state);
                }
                _mediaButtons[mediaKind].Enabled =
                    state != MediaState.Unavailable &&
                    _mediaButtons[mediaKind].DropDownItems.Count > 0;
            }
            UpdateFloppyMenuIndicators(viewModel);
            UpdateSecureDigitalMenuIndicators(viewModel);
            UpdateTapeToolbarState(viewModel);
            UpdateOpticalToolbarState(viewModel);
        }

        private static MediaState GetAggregateMediaState(
            IMainViewModel viewModel,
            MediaStatusKind mediaKind)
        {
            if (viewModel == null)
                return MediaState.Unavailable;
            var count = mediaKind == MediaStatusKind.Floppy ? 4 :
                mediaKind == MediaStatusKind.SecureDigital ? 2 : 0;
            if (count == 0)
                return viewModel.GetMediaState(mediaKind, -1);
            var available = false;
            for (var index = 0; index < count; index++)
            {
                var state = viewModel.GetMediaState(mediaKind, index);
                if (state == MediaState.Mounted)
                    return MediaState.Mounted;
                if (state == MediaState.Empty)
                    available = true;
            }
            return available ? MediaState.Empty : MediaState.Unavailable;
        }

        private void UpdateTapeToolbarState(IMainViewModel viewModel)
        {
            var state = viewModel == null
                ? MediaState.Unavailable
                : viewModel.GetMediaState(MediaStatusKind.Tape, -1);
            if (!_tapeState.HasValue || _tapeState.Value != state)
            {
                _tapeState = state;
                SetSplitButtonImage(_tapeButton, MediaStatusKind.Tape, state);
                ReplaceMenuIndicator(_tapeEjectMenuItem, state);
            }
            _tapeButton.Enabled = state != MediaState.Unavailable;
        }

        private void UpdateOpticalToolbarState(IMainViewModel viewModel)
        {
            var state = viewModel == null
                ? MediaState.Unavailable
                : viewModel.GetMediaState(MediaStatusKind.OpticalDisc, -1);
            if (!_opticalState.HasValue || _opticalState.Value != state)
            {
                _opticalState = state;
                SetSplitButtonImage(
                    _opticalButton,
                    MediaStatusKind.OpticalDisc,
                    state);
                ReplaceMenuIndicator(_opticalEjectMenuItem, state);
            }
            // A disconnected drive is shown gray, but the button remains usable
            // so that the user can connect or select a Windows optical drive.
            _opticalButton.Enabled = _connectOpticalCommand != null &&
                _connectOpticalCommand.CanExecute(this);
        }

        private void UpdateSecureDigitalMenuIndicators(
            IMainViewModel viewModel)
        {
            UpdateMediaMenuIndicators(
                _ejectSdMenuItems,
                _sdMountedStates,
                index => viewModel == null
                    ? MediaState.Unavailable
                    : viewModel.GetMediaState(
                        MediaStatusKind.SecureDigital,
                        index));
        }

        private void UpdateFloppyMenuIndicators(IMainViewModel viewModel)
        {
            UpdateMediaMenuIndicators(
                _ejectFddMenuItems,
                _fddMountedStates,
                index => viewModel == null
                    ? MediaState.Unavailable
                    : viewModel.GetMediaState(MediaStatusKind.Floppy, index));
        }

        private static void UpdateMediaMenuIndicators(
            ToolStripMenuItem[] menuItems,
            MediaState?[] mountedStates,
            Func<int, MediaState> getMountedState)
        {
            for (var index = 0; index < menuItems.Length; index++)
            {
                var item = menuItems[index];
                if (item == null)
                    continue;
                var state = getMountedState(index);
                if (mountedStates[index].HasValue &&
                    mountedStates[index].Value == state)
                    continue;
                mountedStates[index] = state;
                ReplaceMenuIndicator(item, state);
            }
        }

        private static void ReplaceMenuIndicator(
            ToolStripMenuItem item,
            MediaState state)
        {
            if (item == null)
                return;
            var oldImage = item.Image;
            item.Image = CreateMediaMenuIndicator(state);
            if (oldImage != null)
                oldImage.Dispose();
            item.ImageScaling = ToolStripItemImageScaling.None;
        }

        private static Bitmap CreateMediaMenuIndicator(MediaState state)
        {
            const int size = 10;
            var image = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.Magenta);
                graphics.SmoothingMode =
                    System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(GetMediaStateColor(state)))
                {
                    graphics.FillEllipse(brush, 0, 0, size - 1, size - 1);
                }
            }
            image.MakeTransparent(Color.Magenta);
            return image;
        }

        private void RefreshMediaToolbarStates()
        {
            _mediaMountedStates.Clear();
            Array.Clear(_fddMountedStates, 0, _fddMountedStates.Length);
            Array.Clear(_sdMountedStates, 0, _sdMountedStates.Length);
            _tapeState = null;
            _opticalState = null;
            UpdateMediaToolbarStates();
        }

        private void RequestCmosReset()
        {
            var query = _resolver.TryResolve<IUserQuery>();
            if (query == null || query.Show(
                    "Reset CMOS? Machine settings will be returned to their initial state.",
                    "Reset CMOS",
                    DlgButtonSet.YesNo,
                    DlgIcon.Warning) != DlgResult.Yes)
            {
                return;
            }
            var viewModel = DataContext as IMainViewModel;
            if (viewModel != null && viewModel.ResetCmosState())
            {
                RefreshMediaToolbarStates();
            }
            else
            {
                _resolver.Resolve<IUserMessage>().Warning(
                    "The current machine has no resettable CMOS state.");
            }
        }

        private void menuVmCmosReset_Click(object sender, EventArgs e)
        {
            RequestCmosReset();
        }

        private void menuVmFactoryReset_Click(object sender, EventArgs e)
        {
            RequestFactoryReset();
        }

        private void RequestFactoryReset()
        {
            var query = _resolver.TryResolve<IUserQuery>();
            if (query == null || query.Show(
                    "Perform full reset? ZXMAK2 will be returned to its initial state.",
                    "Full reset",
                    DlgButtonSet.YesNo,
                    DlgIcon.Warning) != DlgResult.Yes)
            {
                return;
            }
            var viewModel = DataContext as IMainViewModel;
            if (viewModel == null || !viewModel.PrepareFactoryReset())
            {
                return;
            }
            RefreshMediaToolbarStates();
            Application.Restart();
            Close();
        }

        private void SetMediaToolbarImage(MediaStatusKind mediaKind, MediaState state)
        {
            ToolStripDropDownButton button;
            if (!_mediaButtons.TryGetValue(mediaKind, out button))
            {
                return;
            }
            var oldImage = button.Image;
            button.Image = CreateMediaToolbarImage(mediaKind, state);
            if (oldImage != null)
            {
                oldImage.Dispose();
            }
        }

        private static Bitmap CreateMediaToolbarImage(
            MediaStatusKind mediaKind,
            MediaState state)
        {
            var image = new Bitmap(MediaToolbarArtworkWidth, MediaToolbarArtworkHeight);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.Magenta);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                var artwork = GetMediaArtwork(mediaKind);
                var destination = new Rectangle(
                    0,
                    0,
                    MediaToolbarArtworkWidth,
                    MediaToolbarArtworkHeight);
                if (state == MediaState.Unavailable)
                {
                    using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                    {
                        attributes.SetColorMatrix(
                            new System.Drawing.Imaging.ColorMatrix(new[]
                            {
                                new[] { .30f, .30f, .30f, 0f, 0f },
                                new[] { .59f, .59f, .59f, 0f, 0f },
                                new[] { .11f, .11f, .11f, 0f, 0f },
                                new[] { 0f, 0f, 0f, 1f, 0f },
                                new[] { 0f, 0f, 0f, 0f, 1f },
                            }));
                        graphics.DrawImage(
                            artwork,
                            destination,
                            0,
                            0,
                            artwork.Width,
                            artwork.Height,
                            GraphicsUnit.Pixel,
                            attributes);
                    }
                }
                else
                {
                    graphics.DrawImage(artwork, destination);
                }

                var dotColor = GetMediaStateColor(state);
                var dotX = MediaToolbarArtworkWidth - MediaStatusDotDiameter - 1;
                var dotY = MediaToolbarArtworkHeight - MediaStatusDotDiameter - 1;
                graphics.FillEllipse(Brushes.WhiteSmoke,
                    dotX - 1, dotY - 1,
                    MediaStatusDotDiameter + 2,
                    MediaStatusDotDiameter + 2);
                using (var dotBrush = new SolidBrush(dotColor))
                {
                    graphics.FillEllipse(dotBrush,
                        dotX, dotY,
                        MediaStatusDotDiameter,
                        MediaStatusDotDiameter);
                }
            }
            image.MakeTransparent(Color.Magenta);
            return image;
        }

        private static Image GetMediaArtwork(MediaStatusKind mediaKind)
        {
            switch (mediaKind)
            {
                case MediaStatusKind.SecureDigital:
                    return global::ZXMAK2.Host.WinForms.Properties.Resources.EmuSdImage_104x72;
                case MediaStatusKind.HardDisk:
                    return global::ZXMAK2.Host.WinForms.Properties.Resources.EmuHddImage_104x72;
                case MediaStatusKind.Tape:
                    return global::ZXMAK2.Host.WinForms.Properties.Resources.EmuTapeImage_104x72;
                case MediaStatusKind.OpticalDisc:
                    return global::ZXMAK2.Host.WinForms.Properties.Resources.EmuCdImage_104x72;
                default:
                    return global::ZXMAK2.Host.WinForms.Properties.Resources.EmuFddImage_104x72;
            }
        }

        private static Color GetMediaStateColor(MediaState state)
        {
            switch (state)
            {
                case MediaState.Mounted:
                    return Color.FromArgb(35, 180, 70);
                case MediaState.Empty:
                    return Color.FromArgb(215, 55, 50);
                default:
                    return Color.FromArgb(135, 135, 135);
            }
        }

        private static void SetSplitButtonImage(
            ToolStripSplitButton button,
            MediaStatusKind mediaKind,
            MediaState state)
        {
            if (button == null)
                return;
            var oldImage = button.Image;
            button.Image = CreateMediaToolbarImage(mediaKind, state);
            if (oldImage != null)
                oldImage.Dispose();
        }

        private void QuickBootStateTimer_OnTick(object sender, EventArgs e)
        {
            UpdateQuickBootAvailability();
            UpdateMediaToolbarStates();
        }

        private void MenuTools_OnDropDownOpening(object sender, EventArgs e)
        {
            UpdateQuickBootAvailability();
        }

        private void UpdateQuickBootAvailability()
        {
            var available = CommandQuickLoad != null &&
                CommandQuickLoad.CanExecute(null);
            if (_quickBootAvailable == available)
            {
                return;
            }
            _quickBootAvailable = available;
            if (CommandQuickLoad != null)
            {
                CommandQuickLoad.Update();
            }
            if (available)
            {
                if (!menuTools.DropDownItems.Contains(_menuToolsQuickBoot))
                {
                    menuTools.DropDownItems.Add(_menuToolsQuickBoot);
                    SortMenuTools();
                }
            }
            else
            {
                menuTools.DropDownItems.Remove(_menuToolsQuickBoot);
            }
        }

        #endregion Menu Handlers


        #region Bind Helpers

        private void BindCommand(ToolStripItem target, string path, object parameter = null)
        {
            target.Tag = parameter; // CommandParameter
            _binding.Bind(target, "Command", path);
            _binding.Bind(target, "Text", path + ".Text");
            _binding.Bind(target, "Checked", path + ".Checked");
            _binding.Bind(target, "Visible", path, arg => arg != null);
        }

        private void BindCommandLight(ToolStripItem target, string path, object parameter = null)
        {
            target.Tag = parameter; // CommandParameter
            _binding.Bind(target, "Command", path);
            _binding.Bind(target, "Visible", path, arg => arg != null);
        }

        private static bool CanExecute(ICommand command, object arg)
        {
            return command != null && command.CanExecute(arg);
        }

        private static void OnCommand(ICommand command, object arg = null)
        {
            if (command == null || !command.CanExecute(arg))
            {
                return;
            }
            command.Execute(arg);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion Bind Helpers
    }
}
