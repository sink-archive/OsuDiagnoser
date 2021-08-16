using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OsuDiagnoser
{
	public static class MultiThreadTool
	{
		// Drawback of this implementation: the order of the output will end up a bit mixed up compared to the input
		public static TOut[] BatchTask<TIn, TOut>(TIn[] items, Func<TIn, TOut> processFunc, int threads)
		{
			if (items.Length < threads) threads = items.Length;
			
			var threadBatches = new List<TIn>?[threads];
			for (var i = 0; i < items.Length; i++)
			{
				threadBatches[i % threads] ??= new List<TIn>();
				
				threadBatches[i % threads]!.Add(items[i]);
			}

			var threadTasks = new Task<TOut[]>[threads];
			for (var i = 0; i < threadBatches.Length; i++)
			{
				// if it loops around before this thread is actually started i would be wrong and this line fixes it
				var i1 = i;
				threadTasks[i] = Task.Run(() => QueueProcess(threadBatches[i1]!, processFunc));
			}

			Task.WaitAll(threadTasks.Cast<Task>().ToArray());

			return threadTasks.SelectMany(thread => thread.Result).ToArray();

			static TOut[] QueueProcess(IEnumerable<TIn> items, Func<TIn, TOut> func) => items.Select(func).ToArray();
		}
	}
}