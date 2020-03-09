using CefSharp;
using CefSharp.Wpf;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WhatsappTray2
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private const string AppName = "WhatsappTray";
		private const string Version = "2.0.0.0";

		private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

		readonly TaskbarIcon tbi;
		
		public MainWindow()
		{
			InitializeComponent();

			logger.Info("WhatsappTray2 started");

			// https://www.codeproject.com/Articles/36468/WPF-NotifyIcon-2
			tbi = new TaskbarIcon();
			tbi.Icon = IconDrawing.DrawIcon("0");
			//tbi.ToolTipText = "hello world";
			tbi.MenuActivation = PopupActivationMode.RightClick;
			tbi.ContextMenu = CreateContextMenue();
			tbi.TrayLeftMouseUp += (sender, e) => RestoreWindow();

			Icon = IconDrawing.DrawBitmap("0").ToImageSource();

			// Start peridic timer to update the messages count in the tray-symbol.
			var cancellationToken = new CancellationToken();
			Task.Run(async () =>
			{
				WhatsAppApi.Browser = Browser;

				logger.Info("Waiting for Whatsapp to initialize.");
				await PeriodicWaitForWhatsappInitialized(TimeSpan.FromSeconds(1), cancellationToken);

				WhatsAppApi.Initialize();
				
				// = Start the message-count-update-function =
				await PeriodicMessageCountUpdate(TimeSpan.FromSeconds(1), cancellationToken);
			});

			if (Properties.Settings.Default.StartMinimized == false) {
				this.Show();
			} else {
				// If the Browser is not shown we need to initialize it, otherwise it will not load the webpage.
				Browser.CreateBrowser(null, new Size(20, 20));
			}

			Browser.DownloadHandler = new DownloadHandler();
		}

		public async Task PeriodicWaitForWhatsappInitialized(TimeSpan interval, CancellationToken cancellationToken)
		{
			while (true) {
				logger.Info(nameof(PeriodicWaitForWhatsappInitialized));

				var isInitialized = WhatsAppApi.IsInitialized();
				logger.Info("WhatsAppApi.IsInitialized:" + isInitialized.ToString());

				if (isInitialized) {
					break;
				}

				await Task.Delay(interval, cancellationToken);
			}
		}

		public async Task PeriodicMessageCountUpdate(TimeSpan interval, CancellationToken cancellationToken)
		{
			while (true) {
				logger.Info(nameof(PeriodicMessageCountUpdate));

				var count = WhatsAppApi.GetAllChatsWithNewMsgCount();
				var tempIcon = tbi.Icon;
				tbi.Icon = IconDrawing.DrawIcon(count.ToString());
				tempIcon.Dispose();
				SetIconSafe(count.ToString());
				await Task.Delay(interval, cancellationToken);
			}
		}

		private void RestoreWindow()
		{
			logger.Info(nameof(RestoreWindow));
			this.Show();
			this.WindowState = WindowState.Normal;
			this.Activate();
		}

		private void ShowVersionsInfo()
		{
			logger.Info($"{AppName} {Version}");
			MessageBox.Show($"{AppName} {Version}");
		}

		private void SetIconSafe(string iconText)
		{
			if (this.Dispatcher.CheckAccess() == false) {
				this.Dispatcher.Invoke(new Action<string>(SetIconSafe), iconText);
				return;
			}
			var bitmapTemp = IconDrawing.DrawBitmap(iconText);
			var imageSource = bitmapTemp.ToImageSource();
			Icon = imageSource;
			bitmapTemp.Dispose();
		}

		private void Button_Click_A(object sender, RoutedEventArgs e)
		{
		}

		private void Button_Click_B(object sender, RoutedEventArgs e)
		{
		}

		private void Button_Click_WhatsappTrayInfo(object sender, RoutedEventArgs e)
		{
			ShowVersionsInfo();
		}

		/// <summary>
		/// Handle minimize button of in title-bar
		/// </summary>
		private void Window_StateChanged(object sender, EventArgs e)
		{
			if (Properties.Settings.Default.CloseToTray == false) {
				if (this.WindowState == WindowState.Minimized) {
					this.Hide();
				}
			}
		}

		/// <summary>
		/// Handle close button of in title-bar
		/// </summary>
		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			if (Properties.Settings.Default.CloseToTray == true) {
				e.Cancel = true;
				this.Hide();
			} else {
				base.OnClosing(e);
			}
		}

		private void SetStartup()
		{
			RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

			if (Properties.Settings.Default.LaunchOnWindowsStartup) {
				rk.SetValue(AppName, System.Reflection.Assembly.GetExecutingAssembly().Location);
			} else {
				rk.DeleteValue(AppName, false);
			}
		}

		public ContextMenu CreateContextMenue()
		{
			var contextMenu = new ContextMenu();

			// TODO: Move options to UI?
			contextMenu.AddMenueItemSetting("Close to tray", "CloseToTray");
			contextMenu.AddMenueItemSetting("Launch on Windows startup", "LaunchOnWindowsStartup", (sender, e) => SetStartup());
			contextMenu.AddMenueItemSetting("Start minimized", "StartMinimized");
			contextMenu.Items.Add(new Separator());
			//contextMenu.AddMenueItem("About WhatsappTray", (sender, e) => ShowVersionsInfo());
			contextMenu.AddMenueItem("Restore Window", (sender, e) => RestoreWindow(), false, false);
			contextMenu.AddMenueItem("Close Whatsapp", (sender, e) => Close(), false, false);

			return contextMenu;
		}
	}

	public static class ContextMenuExtensions
	{
		public static void AddMenueItemSetting(this ContextMenu contextMenu, string header, string settingsKey, RoutedEventHandler eventHandler = null)
		{
			var menuItem = new MenuItem() {
				Header = header,
				IsChecked = (bool)Properties.Settings.Default[settingsKey],
				// NOTE: IsCheckable seems to bugged. The inverted value counts!
				IsCheckable = false,
			};

			menuItem.Click += (sender, e) =>
			{
				// Toggle checked-status
				menuItem.IsChecked = !menuItem.IsChecked;

				//logger.Info($"Toggle setting '{settingsKey}' and save.");
				Properties.Settings.Default[settingsKey] = !(bool)Properties.Settings.Default[settingsKey];
				Properties.Settings.Default.Save();

				eventHandler?.Invoke(sender, e);
			};

			contextMenu.Items.Add(menuItem);
		}

		/// <summary>
		/// NOTE: IsCheckable seems to bugged. The inverted value counts!
		/// </summary>
		public static void AddMenueItem(this ContextMenu contextMenu, string header, RoutedEventHandler eventHandler = null, bool isChecked = false, bool isCheckable = true)
		{
			var menuItem = new MenuItem() {
				Header = header,
				IsChecked = isChecked,
				// NOTE: IsCheckable seems to bugged. The inverted value counts!
				IsCheckable = isCheckable,
			};

			menuItem.Click += (sender, e) =>
			{
				if (menuItem.IsCheckable) {
					// Toggle checked-status
					menuItem.IsChecked = !menuItem.IsChecked;
				}
				if (eventHandler != null) {
					eventHandler.Invoke(sender, e);
				}
			};

			contextMenu.Items.Add(menuItem);
		}

	}
}
