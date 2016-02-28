using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ProcInfo4Net
{
	class Program
	{
		static string GetArchitecture(bool is64BitProcess)
		{
			return is64BitProcess ? "x64" : "x86";
		}
		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			if (args == null || args.Length != 1)
			{
				ShowHowToUse();
				return;
			}
			int procId = 0;
			string procIdStr = args.FirstOrDefault();
			if (!int.TryParse(procIdStr, out procId))
			{
				Console.WriteLine("Couldn't parse '{0}' as int, exiting...", procIdStr);
				return;
			}
			var currProc = Process.GetCurrentProcess();
			Console.WriteLine("{0}[{1}] is {2}", Path.GetFileName(currProc.MainModule.FileName), currProc.Id, GetArchitecture(Environment.Is64BitProcess));

			System.Diagnostics.Process process = null;
			bool targetProcIs64BitProcess = false;
			try
			{
				process = System.Diagnostics.Process.GetProcessById(procId);
				targetProcIs64BitProcess = NativeWrapper.Is64BitProcess(process.Handle);
				Console.WriteLine("Successfully connected to {0} process '{1}'[{2}]", GetArchitecture(targetProcIs64BitProcess), process.MainModule.FileName, process.Id);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Couldn't connect to process '{0}'.  Error: {1}", procId, ex);
				return;
			}

			// If current process is x64 and target process is x86, we need restart it as x86 process
			if (Environment.Is64BitProcess && !targetProcIs64BitProcess)
			{
				Console.WriteLine("Need restart it as x86 process");
				string path = currProc.MainModule.FileName;
				string[] splitted = path.Split('.');
				string x86Path = Path.Combine(Path.GetDirectoryName(path), splitted[0] + "_x86." + splitted[1]);
				ProcessStartInfo psi = new ProcessStartInfo(x86Path, procIdStr);
				try { Process.Start(psi); }
				catch (Exception ex)
				{
					Console.WriteLine("Couldn't start x86 process from '{0}'", x86Path);
				}
				return;
			}
			Console.WriteLine("Start working...");
			var now = DateTime.Now.ToString().Replace(":", "_");
			string fileName = string.Format("{0}_{1}_{2}.txt", Path.GetFileName(process.MainModule.FileName), procId, now);
			Console.WriteLine("The result will be saved in: '{0}'", fileName);

			using (process)
			{
				using (var stream = File.OpenWrite(fileName))
				{
					using (TextWriter tw = new StreamWriter(stream))
					{
						using (DataTarget dt = DataTarget.AttachToProcess(procId, 5000, AttachFlag.NonInvasive))
						{
							ClrInfo version = dt.ClrVersions[0];

							tw.WriteLine("ClrVersion: {0}", version);
							Console.WriteLine("ClrVersion: {0}", version);

							tw.WriteLine("Process: {0}\r\nVersion: \r\n{1}", Path.GetFileName(process.MainModule.FileName), process.MainModule.FileVersionInfo);
							Console.WriteLine("Process: {0}\r\nVersion: \r\n{1}", Path.GetFileName(process.MainModule.FileName), process.MainModule.FileVersionInfo);

							EnumerateLoadedModules(tw, dt);
							tw.WriteLine();
							Console.WriteLine();

							ClrRuntime runtime = null;
							try
							{
								runtime = version.CreateRuntime();
							}
							catch (Exception ex)
							{
								Console.WriteLine("Couldn't create ClrRuntime, Exiting... :{0}", ex);
								return;
							}

							ShowThreads(tw, runtime);
							tw.WriteLine();
							Console.WriteLine();

							ShowApplicationDomains(tw, runtime);
						}
					}
				}
			}
			Console.WriteLine("Finished.");
		}
		private static void ShowApplicationDomains(TextWriter textWriter, ClrRuntime runtime)
		{
			textWriter.WriteLine("AppDomains: ");
			Console.WriteLine("AppDomains: ");
			foreach (var appDomain in runtime.AppDomains)
			{
				string domainInfo = string.Format("{0}[{1}] {2}", appDomain.Name, appDomain.Id, appDomain.ApplicationBase);
				textWriter.WriteLine("   {0}", domainInfo);
				Console.WriteLine("   {0}", domainInfo);
			}
		}
		private static void ShowThreads(TextWriter textWriter, ClrRuntime runtime)
		{
			int aliveThreads = 0;
			textWriter.WriteLine("Threads: ");
			Console.WriteLine("Threads: ");
			foreach (ClrThread thread in runtime.Threads)
			{
				if (!thread.IsAlive)
				{
					textWriter.WriteLine("Thread[{0}|{1}] isn't alive", thread.ManagedThreadId, thread.OSThreadId);

					continue;
				}
				aliveThreads++;
				Console.WriteLine("Thread {0:X}:", thread.OSThreadId);
				textWriter.WriteLine("Thread {0:X}:", thread.OSThreadId);
				textWriter.WriteLine("============================================================================================================================================");
				foreach (ClrStackFrame frame in thread.StackTrace)
				{
					Console.WriteLine("{0,12:X} {1,12:X} {2}", frame.StackPointer, frame.InstructionPointer, frame.ToString());
					textWriter.WriteLine("{0,12:X} {1,12:X} {2}", frame.StackPointer, frame.InstructionPointer, frame.ToString());
				}
				textWriter.WriteLine("============================================================================================================================================");
				Console.WriteLine();
				textWriter.WriteLine();
				Console.WriteLine();
			}
			Console.WriteLine("Alive Threads: {0}", aliveThreads);
			textWriter.WriteLine("Alive Threads: {0}", aliveThreads);
		}
		private static void EnumerateLoadedModules(TextWriter textWriter, DataTarget dt)
		{
			try
			{
				var modules = dt.EnumerateModules();
				textWriter.WriteLine("Loaded modules: ");
				Console.WriteLine("Loaded modules: ");
				foreach (var module in modules)
				{
					string moduleInfo = string.Format("{0} {1}", module.FileName, module.Version);
					textWriter.WriteLine("   {0}", moduleInfo);
					Console.WriteLine("   {0}", moduleInfo);
				}
			}
			catch (Exception ex)
			{
				textWriter.WriteLine("Couldn't enumerate modules: {0}", ex.Message);
				Console.WriteLine("Couldn't enumerate modules: {0}", ex.Message);
			}
		}
		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Console.WriteLine("Got unhandled exception: {0}", e.ExceptionObject as Exception);
		}
		private static void ShowHowToUse()
		{
			Console.WriteLine("Incorrect input. Usage: ");
			Console.WriteLine("   ProcInfo4Net.exe ProcessId");
		}
	}
}
