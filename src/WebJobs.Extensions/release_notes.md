## Microsoft.Azure.WebJobs.Extensions <version>

- Added `CancellationToken` overloads to `ScheduleMonitor` (`GetStatusAsync`, `GetSafeStatusAsync`, `UpdateStatusAsync`). `TimerListener.StartAsync` now flows its cancellation token into the pre-invocation status read so an indefinite hang (for example, AAD token acquisition stalling) cannot block the singleton listener lock during host startup. The default implementations of the new overloads delegate to the original methods, so existing subclasses continue to work without changes.
