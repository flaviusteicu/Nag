using System;

namespace Nag.Interfaces
{
    public interface INotificationScheduler
    {
        event EventHandler? OnTriggerNotification;
        event EventHandler<DateTime>? OnNextScheduledTimeCalculated;

        void Start();
        void Stop();
        void ForceRecalculate();
    }
}
