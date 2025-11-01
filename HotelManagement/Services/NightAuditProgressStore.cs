using System;
using System.Collections.Concurrent;
using HotelManagement.Models;

namespace HotelManagement.Services
{
    /// <summary>
    /// Bezpieczny wątkowo magazyn postępów audytu (w pamięci).
    /// </summary>
    public class NightAuditProgressStore
    {
        private readonly ConcurrentDictionary<Guid, NightAuditProgress> _store = new();

        public NightAuditProgress Create()
        {
            var ap = new NightAuditProgress();
            _store[ap.Id] = ap;
            return ap;
        }

        public NightAuditProgress? Get(Guid id)
            => _store.TryGetValue(id, out var ap) ? ap : null;

        public void Update(Guid id, Action<NightAuditProgress> updater)
        {
            if (_store.TryGetValue(id, out var ap))
            {
                lock (ap) { updater(ap); }
            }
        }
    }
}
