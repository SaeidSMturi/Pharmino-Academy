using System.Collections.Concurrent;
using Application.Interfaces;

namespace Infrastructure.Services
{
    public class HlsProgressService : IHlsProgressService
    {
        private readonly ConcurrentDictionary<int, HlsJobState> _jobs = new();

        public void SetQueued(int videoId) =>
            _jobs[videoId] = new HlsJobState { VideoId = videoId, Status = HlsJobStatus.Queued };

        public void Report(int videoId, int percent)
        {
            var job = _jobs.GetOrAdd(videoId, id => new HlsJobState { VideoId = id });
            job.Status = HlsJobStatus.Processing;
            // هرگز 100 گزارش نمی‌دهیم تا SetCompleted بفرستد — از رقابت جلوگیری می‌کند
            job.Percent = Math.Clamp(percent, 0, 99);
        }

        public void SetCompleted(int videoId)
        {
            if (_jobs.TryGetValue(videoId, out var job))
            {
                job.Status = HlsJobStatus.Completed;
                job.Percent = 100;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public void SetFailed(int videoId, string error)
        {
            if (_jobs.TryGetValue(videoId, out var job))
            {
                job.Status = HlsJobStatus.Failed;
                job.Error = error;
                job.CompletedAt = DateTime.UtcNow;
            }
        }

        public HlsJobState? Get(int videoId)
        {
            _jobs.TryGetValue(videoId, out var job);
            return job;
        }

        public void Remove(int videoId) => _jobs.TryRemove(videoId, out _);
    }
}
