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
        private readonly Dictionary<MediaStatusKind, ToolStripDropDownButton> _mediaButtons =
            new Dictionary<MediaStatusKind, ToolStripDropDownButton>();
        private readonly Dictionary<MediaStatusKind, bool?> _mediaMountedStates =
            new Dictionary<MediaStatusKind, bool?>();
        private readonly List<ToolStripItemBindingAdapter> _mediaItemAdapters =
            new List<ToolStripItemBindingAdapter>();
        private bool? _quickBootAvailable;
        private ISuccessCommand _openSdCommand;
        private ISuccessCommand _ejectSdCommand;
        private ISuccessCommand _openHddCommand;
        private ISuccessCommand _ejectHddCommand;
        private readonly ISuccessCommand[] _openFddCommands = new ISuccessCommand[4];
        private readonly ISuccessCommand[] _ejectFddCommands = new ISuccessCommand[4];

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
            _binding.Bind(this, "SelectedScaleMode", "RenderScaleMode");

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
            _binding.Bind(this, "CommandQuickLoad", "CommandQuickLoad");
            _binding.Bind(this, "CommandOpenUri", "CommandOpenUri");
            _binding.Bind(this, "CommandMachineSwitch", "CommandMachineSwitch");
        }


        #region Commands

        public ICommand CommandViewFullScreen { get; set; }
        public ICommand CommandVmPause { get; set; }
        public ICommand CommandVmMaxSpeed { get; set; }
        public ICommand CommandVmWarmReset { get; set; }
        public ICommand CommandTapePause { get; set; }
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
            _openSdCommand = null;
            _ejectSdCommand = null;
            _openHddCommand = null;
            _ejectHddCommand = null;
            Array.Clear(_openFddCommands, 0, _openFddCommands.Length);
            Array.Clear(_ejectFddCommands, 0, _ejectFddCommands.Length);
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
            var successCommand = command as ISuccessCommand;
            if (successCommand == null)
            {
                return false;
            }
            switch (command.Text)
            {
                case "Open SD Card image...":
                    _openSdCommand = successCommand;
                    return true;
                case "Eject SD Card":
                    _ejectSdCommand = successCommand;
                    return true;
                case "Open HDD image...":
                    _openHddCommand = successCommand;
                    return true;
                case "Eject HDD":
                    _ejectHddCommand = successCommand;
                    return true;
            }
            for (var drive = 0; drive < 4; drive++)
            {
                var name = (char)('A' + drive);
                if (command.Text == string.Format("Load FDD {0}:...", name))
                {
                    _openFddCommands[drive] = successCommand;
                    return true;
                }
                if (command.Text == string.Format("Eject FDD {0}:", name))
                {
                    _ejectFddCommands[drive] = successCommand;
                    return true;
                }
            }
            return false;
        }

        private void InitializeMediaToolbar()
        {
            var index = tbrStrip.Items.IndexOf(tbrButtonSdImage);
            tbrStrip.Items.Remove(tbrButtonSdImage);
            tbrButtonSdImage.Dispose();
            AddMediaToolbarButton(MediaStatusKind.Floppy, "Floppy disk images", index++);
            AddMediaToolbarButton(MediaStatusKind.HardDisk, "HDD image", index++);
            AddMediaToolbarButton(MediaStatusKind.SecureDigital, "SD card image", index);
        }

        private void AddMediaToolbarButton(MediaStatusKind mediaKind, string toolTip, int index)
        {
            var button = new ToolStripDropDownButton();
            button.DisplayStyle = ToolStripItemDisplayStyle.Image;
            button.ImageTransparentColor = Color.Magenta;
            button.AutoSize = false;
            button.Size = new Size(45, 36);
            button.Text = toolTip;
            button.ToolTipText = toolTip;
            button.Enabled = false;
            _mediaButtons.Add(mediaKind, button);
            tbrStrip.Items.Insert(index, button);
            SetMediaToolbarImage(mediaKind, false);
        }

        private void ClearMediaToolbarMenus()
        {
            _mediaItemAdapters.ForEach(adapter => adapter.Dispose());
            _mediaItemAdapters.Clear();
            foreach (var button in _mediaButtons.Values)
            {
                button.DropDownItems.Clear();
                button.Enabled = false;
            }
        }

        private void RebuildMediaToolbarMenus()
        {
            ClearMediaToolbarMenus();
            AddMediaMenuItem(MediaStatusKind.SecureDigital, "Load SD", _openSdCommand, true);
            AddMediaMenuItem(MediaStatusKind.SecureDigital, "Eject SD", _ejectSdCommand, true);
            AddMediaMenuItem(MediaStatusKind.HardDisk, "Load HDD", _openHddCommand, true);
            AddMediaMenuItem(MediaStatusKind.HardDisk, "Eject HDD", _ejectHddCommand, true);
            for (var drive = 0; drive < 4; drive++)
            {
                AddMediaMenuItem(
                    MediaStatusKind.Floppy,
                    string.Format("Load {0}:", (char)('A' + drive)),
                    _openFddCommands[drive],
                    false);
                AddMediaMenuItem(
                    MediaStatusKind.Floppy,
                    string.Format("Eject {0}:", (char)('A' + drive)),
                    _ejectFddCommands[drive],
                    false);
            }
            foreach (var pair in _mediaButtons)
            {
                pair.Value.Enabled = pair.Value.DropDownItems.Count > 0;
            }
            UpdateMediaToolbarStates();
        }

        private void AddMediaMenuItem(
            MediaStatusKind mediaKind,
            string text,
            ISuccessCommand sourceCommand,
            bool requiresPowerCycle)
        {
            if (sourceCommand == null)
            {
                return;
            }
            var item = new ToolStripMenuItem(text);
            var command = new CommandDelegate(
                arg => ExecuteMediaCommand(sourceCommand, arg, requiresPowerCycle),
                arg => sourceCommand.CanExecute(arg),
                text);
            var adapter = new ToolStripItemBindingAdapter(item);
            adapter.CommandParameter = this;
            adapter.Command = command;
            _mediaItemAdapters.Add(adapter);
            _mediaButtons[mediaKind].DropDownItems.Add(item);
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
                var isMounted = viewModel != null && viewModel.IsMediaMounted(mediaKind);
                bool? lastState;
                if (_mediaMountedStates.TryGetValue(mediaKind, out lastState) &&
                    lastState.HasValue && lastState.Value == isMounted)
                {
                    continue;
                }
                _mediaMountedStates[mediaKind] = isMounted;
                SetMediaToolbarImage(mediaKind, isMounted);
            }
        }

        private void RequestCmosReset()
        {
            var query = _resolver.TryResolve<IUserQuery>();
            if (query == null || query.Show(
                    "Reset the persistent CMOS configuration? A timestamped backup will be created.",
                    "Reset CMOS",
                    DlgButtonSet.YesNo,
                    DlgIcon.Warning) != DlgResult.Yes)
            {
                return;
            }
            var viewModel = DataContext as IMainViewModel;
            if (viewModel == null || !viewModel.ResetCmosState())
            {
                _resolver.Resolve<IUserMessage>().Warning(
                    "The current machine has no resettable CMOS state.");
            }
        }

        private void RequestFactoryReset()
        {
            var query = _resolver.TryResolve<IUserQuery>();
            if (query == null || query.Show(
                    "Reset saved machine state and restart ZXMAK2? A timestamped backup will be created. Media images and ROM files are not deleted.",
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
            Application.Restart();
            Close();
        }

        private void SetMediaToolbarImage(MediaStatusKind mediaKind, bool isMounted)
        {
            ToolStripDropDownButton button;
            if (!_mediaButtons.TryGetValue(mediaKind, out button))
            {
                return;
            }
            var oldImage = button.Image;
            button.Image = CreateMediaToolbarImage(mediaKind, isMounted);
            if (oldImage != null)
            {
                oldImage.Dispose();
            }
        }

        private static Bitmap CreateMediaToolbarImage(
            MediaStatusKind mediaKind,
            bool isMounted)
        {
            var image = new Bitmap(32, 32);
            using (var graphics = Graphics.FromImage(image))
            using (var outline = new Pen(Color.FromArgb(45, 55, 70), 2))
            using (var detail = new Pen(Color.FromArgb(215, 225, 235), 1))
            {
                graphics.Clear(Color.Magenta);
                if (mediaKind == MediaStatusKind.SecureDigital)
                {
                    graphics.DrawImage(
                        global::ZXMAK2.Host.WinForms.Properties.Resources.EmuSdImage_32x32,
                        new Rectangle(0, 0, 32, 32));
                }
                else if (mediaKind == MediaStatusKind.HardDisk)
                {
                    // Compact blue hard-drive case, platter and arm. The
                    // intentionally small palette matches the legacy toolbar
                    // rather than introducing a photo-style asset.
                    using (var caseBrush = new SolidBrush(Color.FromArgb(40, 96, 160)))
                    using (var sideBrush = new SolidBrush(Color.FromArgb(27, 57, 102)))
                    using (var platterBrush = new SolidBrush(Color.FromArgb(205, 214, 218)))
                    using (var hubBrush = new SolidBrush(Color.FromArgb(82, 130, 184)))
                    {
                        graphics.FillRectangle(sideBrush, 4, 8, 24, 18);
                        graphics.FillRectangle(caseBrush, 4, 5, 24, 19);
                        graphics.DrawRectangle(outline, 4, 5, 24, 19);
                        graphics.FillEllipse(platterBrush, 7, 8, 14, 14);
                        graphics.DrawEllipse(outline, 7, 8, 14, 14);
                        graphics.FillEllipse(hubBrush, 12, 13, 4, 4);
                        graphics.DrawLine(outline, 21, 10, 25, 18);
                        graphics.DrawLine(outline, 20, 16, 25, 18);
                        graphics.FillRectangle(Brushes.WhiteSmoke, 6, 24, 5, 2);
                        graphics.FillRectangle(Brushes.WhiteSmoke, 13, 24, 9, 2);
                    }
                }
                else
                {
                    // Floppy drive front: blue enclosure, dark disk slot,
                    // eject key and a small activity LED.
                    using (var caseBrush = new SolidBrush(Color.FromArgb(45, 112, 151)))
                    using (var slotBrush = new SolidBrush(Color.FromArgb(45, 55, 70)))
                    using (var ejectBrush = new SolidBrush(Color.FromArgb(170, 199, 220)))
                    using (var ledBrush = new SolidBrush(Color.FromArgb(40, 180, 70)))
                    {
                        graphics.FillRectangle(caseBrush, 3, 7, 26, 18);
                        graphics.DrawRectangle(outline, 3, 7, 26, 18);
                        graphics.FillRectangle(slotBrush, 6, 11, 19, 6);
                        graphics.DrawRectangle(outline, 6, 11, 19, 6);
                        graphics.FillRectangle(ejectBrush, 18, 19, 6, 3);
                        graphics.DrawRectangle(outline, 18, 19, 6, 3);
                        graphics.FillEllipse(ledBrush, 7, 20, 3, 3);
                        graphics.DrawLine(detail, 7, 9, 25, 9);
                    }
                }

                var dotColor = isMounted ?
                    Color.FromArgb(35, 180, 70) :
                    Color.FromArgb(215, 55, 50);
                var dotX = 32 - MediaStatusDotDiameter - 1;
                var dotY = 32 - MediaStatusDotDiameter - 1;
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
