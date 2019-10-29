using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CheckoutAndBuild2.Contracts;
using FG.CheckoutAndBuild2.Common;
using FG.CheckoutAndBuild2.Types;

namespace FG.CheckoutAndBuild2.Extensions
{

	public static class ModelExtensions
	{
        static readonly ConcurrentDictionary<string, Solution> cache = new ConcurrentDictionary<string, Solution>();

		public static Solution ToSolution(this ISolutionProjectModel model)
		{
		    if (cache.ContainsKey(model.ItemPath))
		        return cache[model.ItemPath];
			var res =  new Solution(model.ItemPath);
            cache.AddOrUpdate(model.ItemPath, res);
            return res;
		}
		
		public static IEnumerable<Process> FindRunningInstances(this ProcessStartInfo info)
		{			
			return from process in FileHelper.GetProcesses()
					where process?.Path != null && process.Path.ToLower() == info.FileName.ToLower()
					select process.Process;
		}

		public static string EnsureAbsolutePath(this ISolutionProjectModel projectViewModel, string relativeOrAbsoluteFilePath)
		{
			if (File.Exists(relativeOrAbsoluteFilePath) || relativeOrAbsoluteFilePath.Contains(":\\"))
				return relativeOrAbsoluteFilePath;
			return Check.TryCatch<string, Exception>(() => FileHelper.GetAbsolutePath(relativeOrAbsoluteFilePath, projectViewModel.SolutionFolder));
		}

		public static async Task<IEnumerable<Process>> FindRunningInstancesAsync(this ISolutionProjectModel model, 
            bool findLockingOwner,
            CancellationToken cancellationToken = default (CancellationToken))
		{			
			return await Task.Run(() =>
			{				
				var result = model.GetSolutionProjects()
					.Select(project => project.GetExecutable())
					.Where(processStartInfo => processStartInfo != null).ToList()
					.SelectMany(info => info.FindRunningInstances()).Where(process => process != null);
			    if (findLockingOwner)
                    return result.Concat(model.GetSolutionProjects().Select(project => project.GetOutputFile()).Where(info => info.Exists).SelectMany(info => FileHelper.WhoIsLocking(info.FullName, true)));
                return result;			    
			}, cancellationToken);
		}

		public static async Task KillRunningInstancesAsync(this ISolutionProjectModel model,
             bool killLockingOwner,
            CancellationToken cancellationToken = default (CancellationToken))
		{
			(await model.FindRunningInstancesAsync(killLockingOwner, cancellationToken)).Apply(process => process.Kill());
		}

        public static async Task<bool> HasRunningInstancesAsync(this ISolutionProjectModel model, bool findLockingOwner, CancellationToken cancellationToken = default(CancellationToken))
        {
            return (await model.FindRunningInstancesAsync(findLockingOwner, cancellationToken)).Any();
        }

        public static Task StartAsync(this ISolutionProjectModel model, bool attachDebugger, 
            CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Run(async () =>
			{
				foreach (ProcessStartInfo processStartInfo in model.GetSolutionProjects().Select(project => project.GetExecutable()).Where(processStartInfo => processStartInfo != null)) { 
                    Output.WriteLine($"Starting {processStartInfo.FileName}");
					var task = ScriptHelper.ExecuteScriptAsync(processStartInfo.FileName, processStartInfo.Arguments, ScriptExecutionSettings.NormalProcess, cancellationToken: cancellationToken);
				    if (attachDebugger)
				    {
				        var result = await task;
				        DebugHelper.AttachProcess(result.Process.Id);
				    }
				}
			}, cancellationToken);
		}

		public static void SetOperations(this IEnumerable<ISolutionProjectModel> models, OperationInfo info)
		{
			foreach (ISolutionProjectModel projectViewModel in models)
				projectViewModel.CurrentOperation = info;
		}
	}
}