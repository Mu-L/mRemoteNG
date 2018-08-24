using System;
using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using mRemoteNG.App;
using System.Threading;
using System.Globalization;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.App.Info;
using mRemoteNG.Messages;
using mRemoteNG.Tools;
using mRemoteNG.UI.Controls;
using mRemoteNG.UI.Forms;


namespace mRemoteNG.Config.Settings
{
    public class SettingsLoader
	{
        private readonly ExternalAppsLoader _externalAppsLoader;
        private readonly MessageCollector _messageCollector;
	    private readonly MenuStrip _mainMenu;
        private readonly QuickConnectToolStrip _quickConnectToolStrip;
        private readonly ExternalToolsToolStrip _externalToolsToolStrip;
	    private readonly MultiSshToolStrip _multiSshToolStrip;
	    private readonly Func<NotificationAreaIcon> _notificationAreaIconBuilder;
	    private readonly FrmMain _frmMain;


	    public SettingsLoader(
            FrmMain mainForm, 
            MessageCollector messageCollector, 
            QuickConnectToolStrip quickConnectToolStrip, 
            ExternalToolsToolStrip externalToolsToolStrip,
            MultiSshToolStrip multiSshToolStrip,
	        ExternalAppsLoader externalAppsLoader,
	        Func<NotificationAreaIcon> notificationAreaIconBuilder,
            MenuStrip mainMenu)
		{
		    _frmMain = mainForm.ThrowIfNull(nameof(mainForm));
	        _messageCollector = messageCollector.ThrowIfNull(nameof(messageCollector));
	        _quickConnectToolStrip = quickConnectToolStrip.ThrowIfNull(nameof(quickConnectToolStrip));
	        _externalToolsToolStrip = externalToolsToolStrip.ThrowIfNull(nameof(externalToolsToolStrip));
		    _multiSshToolStrip = multiSshToolStrip.ThrowIfNull(nameof(multiSshToolStrip));
		    _externalAppsLoader = externalAppsLoader.ThrowIfNull(nameof(externalAppsLoader));
		    _notificationAreaIconBuilder = notificationAreaIconBuilder.ThrowIfNull(nameof(notificationAreaIconBuilder));
		    _mainMenu = mainMenu.ThrowIfNull(nameof(mainMenu));
		}
        
        #region Public Methods
        public void LoadSettings()
		{
			try
			{
                EnsureSettingsAreSavedInNewestVersion();

                SetSupportedCulture(); 
                SetApplicationWindowPositionAndSize();
                SetKioskMode();

                SetPuttyPath();
                SetShowSystemTrayIcon();
                SetAutoSave();
				LoadExternalAppsFromXml();
                SetAlwaysShowPanelTabs();
						
				if (mRemoteNG.Settings.Default.ResetToolbars)
                    SetToolbarsDefault();
				else
                    LoadToolbarsFromSettings();
			}
			catch (Exception ex)
			{
                _messageCollector.AddExceptionMessage("Loading settings failed", ex);
			}
		}

        private void SetAlwaysShowPanelTabs()
        {
            if (mRemoteNG.Settings.Default.AlwaysShowPanelTabs)
                _frmMain.pnlDock.DocumentStyle = DocumentStyle.DockingWindow;
        }

 

        private void SetSupportedCulture()
        {
            if (mRemoteNG.Settings.Default.OverrideUICulture == "" ||
                !SupportedCultures.IsNameSupported(mRemoteNG.Settings.Default.OverrideUICulture)) return;
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(mRemoteNG.Settings.Default.OverrideUICulture);
            _messageCollector.AddMessage(MessageClass.InformationMsg, $"Override Culture: {Thread.CurrentThread.CurrentUICulture.Name}/{Thread.CurrentThread.CurrentUICulture.NativeName}", true);
        }

        private void SetApplicationWindowPositionAndSize()
        {
            _frmMain.WindowState = FormWindowState.Normal;
            if (mRemoteNG.Settings.Default.MainFormState == FormWindowState.Normal)
            {
                if (!mRemoteNG.Settings.Default.MainFormLocation.IsEmpty)
                    _frmMain.Location = mRemoteNG.Settings.Default.MainFormLocation;
                if (!mRemoteNG.Settings.Default.MainFormSize.IsEmpty)
                    _frmMain.Size = mRemoteNG.Settings.Default.MainFormSize;
            }
            else
            {
                if (!mRemoteNG.Settings.Default.MainFormRestoreLocation.IsEmpty)
                    _frmMain.Location = mRemoteNG.Settings.Default.MainFormRestoreLocation;
                if (!mRemoteNG.Settings.Default.MainFormRestoreSize.IsEmpty)
                    _frmMain.Size = mRemoteNG.Settings.Default.MainFormRestoreSize;
            }

            if (mRemoteNG.Settings.Default.MainFormState == FormWindowState.Maximized)
            {
                _frmMain.WindowState = FormWindowState.Maximized;
            }

            // Make sure the form is visible on the screen
            const int minHorizontal = 300;
            const int minVertical = 150;
            var screenBounds = Screen.FromHandle(_frmMain.Handle).Bounds;
            var newBounds = _frmMain.Bounds;

            if (newBounds.Right < screenBounds.Left + minHorizontal)
                newBounds.X = screenBounds.Left + minHorizontal - newBounds.Width;
            if (newBounds.Left > screenBounds.Right - minHorizontal)
                newBounds.X = screenBounds.Right - minHorizontal;
            if (newBounds.Bottom < screenBounds.Top + minVertical)
                newBounds.Y = screenBounds.Top + minVertical - newBounds.Height;
            if (newBounds.Top > screenBounds.Bottom - minVertical)
                newBounds.Y = screenBounds.Bottom - minVertical;

            _frmMain.Location = newBounds.Location;
        }

        private void SetAutoSave()
        {
            if (mRemoteNG.Settings.Default.AutoSaveEveryMinutes <= 0) return;
            _frmMain.tmrAutoSave.Interval = mRemoteNG.Settings.Default.AutoSaveEveryMinutes * 60000;
            _frmMain.tmrAutoSave.Enabled = true;
        }

        private void SetKioskMode()
        {
            if (!mRemoteNG.Settings.Default.MainFormKiosk) return;
            _frmMain.Fullscreen.Value = true;
        }

        private void SetShowSystemTrayIcon()
        {
            if (mRemoteNG.Settings.Default.ShowSystemTrayIcon)
                Runtime.NotificationAreaIcon = _notificationAreaIconBuilder();
        }

        private void SetPuttyPath()
        {
            PuttyBase.PuttyPath = mRemoteNG.Settings.Default.UseCustomPuttyPath ? mRemoteNG.Settings.Default.CustomPuttyPath : GeneralAppInfo.PuttyPath;
        }

        private void EnsureSettingsAreSavedInNewestVersion()
        {
            if (mRemoteNG.Settings.Default.DoUpgrade)
                UpgradeSettingsVersion();
        }

        private void UpgradeSettingsVersion()
        {
            try
            {
                mRemoteNG.Settings.Default.Save();
                mRemoteNG.Settings.Default.Upgrade();
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("Settings.Upgrade() failed", ex);
            }
            mRemoteNG.Settings.Default.DoUpgrade = false;

            // Clear pending update flag
            // This is used for automatic updates, not for settings migration, but it
            // needs to be cleared here because we know that we just updated.
            mRemoteNG.Settings.Default.UpdatePending = false;
        }

	    private void SetToolbarsDefault()
		{
			ToolStripPanelFromString("top").Join(_quickConnectToolStrip, new Point(300, 0));
            _quickConnectToolStrip.Visible = true;
			ToolStripPanelFromString("bottom").Join(_externalToolsToolStrip, new Point(3, 0));
            _externalToolsToolStrip.Visible = false;
		}

	    private void LoadToolbarsFromSettings()
		{
            ResetAllToolbarLocations();
		    AddMainMenuPanel();
            AddExternalAppsPanel();
		    AddQuickConnectPanel();
		    AddMultiSshPanel();
        }

        /// <summary>
        /// This prevents odd positioning issues due to toolbar load order.
        /// Since all toolbars start in this temp panel, no toolbar load
        /// can be blocked by pre-existing toolbars.
        /// </summary>
	    private void ResetAllToolbarLocations()
	    {
	        var tempToolStrip = new ToolStripPanel();
            tempToolStrip.Join(_mainMenu);
	        tempToolStrip.Join(_quickConnectToolStrip);
	        tempToolStrip.Join(_externalToolsToolStrip);
	        tempToolStrip.Join(_multiSshToolStrip);
        }

	    private void AddMainMenuPanel()
	    {
	        SetToolstripGripStyle(_mainMenu);
            var toolStripPanel = ToolStripPanelFromString("top");
	        toolStripPanel.Join(_mainMenu, new Point(3, 0));
        }

		private void AddQuickConnectPanel()
		{
		    SetToolstripGripStyle(_quickConnectToolStrip);
            _quickConnectToolStrip.Visible = mRemoteNG.Settings.Default.QuickyTBVisible;
            var toolStripPanel = ToolStripPanelFromString(mRemoteNG.Settings.Default.QuickyTBParentDock);
            toolStripPanel.Join(_quickConnectToolStrip, mRemoteNG.Settings.Default.QuickyTBLocation);
		}
		
		private void AddExternalAppsPanel()
		{
		    SetToolstripGripStyle(_externalToolsToolStrip);
            _externalToolsToolStrip.Visible = mRemoteNG.Settings.Default.ExtAppsTBVisible;
            var toolStripPanel = ToolStripPanelFromString(mRemoteNG.Settings.Default.ExtAppsTBParentDock);
            toolStripPanel.Join(_externalToolsToolStrip, mRemoteNG.Settings.Default.ExtAppsTBLocation);
		}

	    private void AddMultiSshPanel()
	    {
	        SetToolstripGripStyle(_multiSshToolStrip);
	        _multiSshToolStrip.Visible = mRemoteNG.Settings.Default.MultiSshToolbarVisible;
            var toolStripPanel = ToolStripPanelFromString(mRemoteNG.Settings.Default.MultiSshToolbarParentDock);
            toolStripPanel.Join(_multiSshToolStrip, mRemoteNG.Settings.Default.MultiSshToolbarLocation);
	    }

	    private void SetToolstripGripStyle(ToolStrip toolbar)
	    {
	        toolbar.GripStyle = mRemoteNG.Settings.Default.LockToolbars
	            ? ToolStripGripStyle.Hidden
	            : ToolStripGripStyle.Visible;
        }
		
		private ToolStripPanel ToolStripPanelFromString(string panel)
		{
			switch (panel.ToLower())
			{
				case "top":
					return _frmMain.tsContainer.TopToolStripPanel;
				case "bottom":
					return _frmMain.tsContainer.BottomToolStripPanel;
				case "left":
					return _frmMain.tsContainer.LeftToolStripPanel;
				case "right":
					return _frmMain.tsContainer.RightToolStripPanel;
				default:
					return _frmMain.tsContainer.TopToolStripPanel;
			}
		}

	    private void LoadExternalAppsFromXml()
		{
            _externalAppsLoader.LoadExternalAppsFromXML();
        }
        #endregion
	}
}
