using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.MAUI.Implementations {
   /// <summary>
   /// Implements IWorkDispatcher for MAUI. Routes UI work to the main thread
   /// and background work to the thread pool, mirroring the WPF Dispatcher behaviour.
   /// </summary>
   public class MauiDispatcher : IWorkDispatcher {
      public async Task WaitForRenderingAsync() {
         // Yield to the main thread so pending UI frames can complete
         await Task.Delay(16); // ~1 frame at 60fps
      }

      public void BlockOnUIWork(Action action) {
         if (MainThread.IsMainThread) {
            action();
         } else {
            MainThread.InvokeOnMainThreadAsync(action).GetAwaiter().GetResult();
         }
      }

      public Task DispatchWork(Action action) {
         return MainThread.InvokeOnMainThreadAsync(action);
      }

      public Task RunBackgroundWork(Action action) {
         return Task.Run(action);
      }

      public IDelayWorkTimer CreateDelayTimer() => new MauiDelayTimer();
   }

   /// <summary>
   /// Implements IDelayWorkTimer using a MAUI-compatible timer.
   /// Debounces rapid property changes (e.g., scrolling) to avoid UI floods.
   /// </summary>
   public class MauiDelayTimer : IDelayWorkTimer {
      private System.Threading.Timer? timer;
      private Action? pendingAction;
      private readonly object lockObj = new();

      public bool HasScheduledWork {
         get { lock (lockObj) return pendingAction != null; }
      }

      public DelayWorkResult DelayCall(TimeSpan delay, Action action) {
         lock (lockObj) {
            var hadPending = pendingAction != null;
            pendingAction = action;

            timer?.Change(Timeout.Infinite, Timeout.Infinite); // cancel any pending fire
            timer = new System.Threading.Timer(_ => {
               Action? toRun;
               lock (lockObj) {
                  toRun = pendingAction;
                  pendingAction = null;
               }
               if (toRun != null) {
                  MainThread.BeginInvokeOnMainThread(toRun);
               }
            }, null, (long)delay.TotalMilliseconds, System.Threading.Timeout.Infinite);

            return hadPending
               ? DelayWorkResult.WorkScheduledAndPreviousWorkCleared
               : DelayWorkResult.WorkScheduled;
         }
      }

      public void Reset() {
         lock (lockObj) {
            timer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            pendingAction = null;
         }
      }

      private static readonly int Timeout = System.Threading.Timeout.Infinite;
   }
}
