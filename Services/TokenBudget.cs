using System.Collections.Concurrent;

namespace duo_code.Services
{
    public sealed class TokenBudget
    {
        private readonly TimeSpan _window = TimeSpan.FromMinutes(1);

        private readonly ConcurrentQueue<(DateTimeOffset ts, int tokens)> _events = new();
        private int _inWindowTotal;

        private int _tpmLimit;

        private DateTimeOffset? _serverWindowEnd;

        public TokenBudget(int initialTpmLimit)
        {
            if (initialTpmLimit <= 0) throw new ArgumentOutOfRangeException(nameof(initialTpmLimit));
            _tpmLimit = initialTpmLimit;
        }

        public int TpmLimit => Volatile.Read(ref _tpmLimit);

        public int AvailableNow
        {
            get
            {
                TrimOld();
                var limit = Volatile.Read(ref _tpmLimit);
                return Math.Max(0, limit - Volatile.Read(ref _inWindowTotal));
            }
        }

        public bool TryReserve(int tokens)
        {
            if (tokens <= 0) return true;
            TrimOld();

            while (true)
            {
                var limit = Volatile.Read(ref _tpmLimit);
                var current = Volatile.Read(ref _inWindowTotal);
                if (current + tokens > limit) return false;

                if (Interlocked.CompareExchange(ref _inWindowTotal, current + tokens, current) == current)
                {
                    _events.Enqueue((DateTimeOffset.UtcNow, tokens));
                    return true;
                }
            }
        }

        public void Release(int tokens)
        {
            if (tokens <= 0) return;
            _events.Enqueue((DateTimeOffset.UtcNow, -tokens));
            Interlocked.Add(ref _inWindowTotal, -tokens);
            TrimOld();
        }

        public void SyncFromHeaders(int? limitTokens, int? remainingTokens, TimeSpan? resetDelay)
        {
            if (limitTokens is > 0)
                Interlocked.Exchange(ref _tpmLimit, limitTokens.Value);

            if (resetDelay is { } d && d > TimeSpan.Zero)
                _serverWindowEnd = DateTimeOffset.UtcNow + d;

            if (limitTokens is > 0 && remainingTokens is >= 0)
            {
                var used = Math.Max(0, limitTokens.Value - remainingTokens.Value);

                while (_events.TryDequeue(out _)) { /* drop */ }
                Interlocked.Exchange(ref _inWindowTotal, used);

                if (used > 0) _events.Enqueue((DateTimeOffset.UtcNow, used));
            }
        }

        private void TrimOld()
        {
            if (_serverWindowEnd is { } end && DateTimeOffset.UtcNow >= end)
            {
                while (_events.TryDequeue(out _)) { /* drop */ }
                Interlocked.Exchange(ref _inWindowTotal, 0);
                _serverWindowEnd = null;
                return;
            }

            var cutoff = DateTimeOffset.UtcNow - _window;
            while (_events.TryPeek(out var e) && e.ts < cutoff)
            {
                if (_events.TryDequeue(out var d))
                    Interlocked.Add(ref _inWindowTotal, -d.tokens);
            }
        }
    }

    public static class TokenBudgets
    {
        private static readonly ConcurrentDictionary<string, TokenBudget> _byKey = new();

        public static TokenBudget Get(string key, int initialTpmLimit)
            => _byKey.GetOrAdd(key, _ => new TokenBudget(initialTpmLimit));
    }
}