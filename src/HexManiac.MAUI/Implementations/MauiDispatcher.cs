using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.MAUI.Implementations {
   public class MauiDispatcher : IWorkDispatcher {
      public async Task WaitForRenderingAsync() => await Task.Delay(16);
      public void BlockOnUIWork(Action action) {
         if (MainThread.IsMainThread) action();
         else MainThread.InvokeOnMainThreadAsync(action).GetAwaiter().GetResult();
      }
      public Task DispatchWork(Action action) => MainThread.InvokeOnMainThreadAsync(action);
      public Task RunBackgroundWork(Action action) => Task.Run(action);
      public IDelayWorkTimer CreateDelayTimer() => new MauiDelayTimer();
   }

   public class MauiDelayTimer : IDelayWorkTimer {
      private Timer timer;
      private Action pendingAction;
      private readonly object lockObj = new();

      public bool HasScheduledWork { get { lock (lockObj) return pendingAction != null; } }

      public DelayWorkResult DelayCall(TimeSpan delay, Action action) {
         lock (lockObj) {
            bool hadPending = pendingAction != null;
            pendingAction = action;
            timer?.Change(Timeout.Infinite, Timeout.Infinite);
            timer = new Timer(_ => {
               Action toRun;
               lock (lockObj) { toRun = pendingAction; pendingAction = null; }
               if (toRun != null) MainThread.BeginInvokeOnMainThread(toRun);
            }, null, (int)delay.TotalMilliseconds, Timeout.Infinite);
            return hadPending
               ? DelayWorkResult.WorkScheduledAndPreviousWorkCleared
               : DelayWorkResult.WorkScheduled;
         }
      }

      public void Reset() {
         lock (lockObj) {
            timer?.Change(Timeout.Infinite, Timeout.Infinite);
            pendingAction = null;
         }
      }
   }
}
