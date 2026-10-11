## Microsoft.Azure.WebJobs.Extensions.Timers.Storage <version>

- Updated `StorageScheduleMonitor` to honor the `CancellationToken` flowed from `TimerListener` through the new `ScheduleMonitor` overloads in `Microsoft.Azure.WebJobs.Extensions` 5.3.0. Blob `DownloadAsync`, `UploadAsync`, and `CreateIfNotExistsAsync` calls now observe the token, preventing host startup from hanging indefinitely (for example, on AAD token acquisition stalls) and holding the singleton listener lock.
- Bumped the `Microsoft.Azure.WebJobs.Extensions` dependency from 5.0.0 to 5.3.0.
