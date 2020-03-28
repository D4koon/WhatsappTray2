using CefSharp;
using CefSharp.Wpf;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WhatsappTray2
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private static NLog.Logger logger = LogManager.GetCurrentClassLogger();

		MainWindow mainWindow;

		void App_Startup(object sender, StartupEventArgs e)
		{
			var settings = new CefSettings();

			// Increase the log severity so CEF outputs detailed information, useful for debugging
			settings.LogSeverity = LogSeverity.Verbose;
			// By default CEF uses an in memory cache, to save cached data e.g. passwords you need to specify a cache path
			// NOTE: The executing user must have sufficient privileges to write to this folder.
			settings.CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhatsappTray\\Cache");

			settings.RemoteDebuggingPort = 8088;

			Cef.Initialize(settings);



			mainWindow = new MainWindow();


			ConfigureNLog(mainWindow);

			// Custom exception-handler to log them.
			AppDomain currentDomain = AppDomain.CurrentDomain;
			currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);
			currentDomain.FirstChanceException += FirstChanceExceptionHandler;
		}

		void ConfigureNLog(MainWindow window)
		{
			LogManager.ThrowExceptions = true;

			var config = new NLog.Config.LoggingConfiguration();

			// Targets where to log to: File and Console
			var logconsole = new NLog.Targets.DebuggerTarget("logconsole");
			//var logMethod = new NLog.Targets.MethodCallTarget("logMethod", window.ProcessLog);
			var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "WAT_logfile.txt" };

			// Rules for mapping loggers to targets            
			config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
			//config.AddRule(LogLevel.Debug, LogLevel.Fatal, logMethod);
			config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

			// Apply config           
			NLog.LogManager.Configuration = config;
		}

		private void FirstChanceExceptionHandler(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs args)
		{
			Exception e = args.Exception;
			logger.Fatal("FirstChanceException caught: " + e.Message);
		}

		static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
		{
			Exception e = (Exception)args.ExceptionObject;
			logger.Fatal("UnhandledExceptionHandler caught : " + e.Message);
			logger.Fatal("Runtime terminating: {0}", args.IsTerminating);
		}

		private void Application_Exit(object sender, ExitEventArgs e)
		{
			Debug.WriteLine("=== Shutdown - after this only cleanup should happen and then exit application ===");
			// == Shutdown NLog ==
			// According to NLog-docu, it is reccomended to call this method when exiting.
			NLog.LogManager.Shutdown(); // Flush and close down internal threads and timers
		}
	}

}
