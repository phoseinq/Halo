using System;

namespace Halo.Widgets;

internal struct DlRate
{

    internal const long StallMs = 2500;

    private long _bytes;
    private long _movedAt;
    private float _rate;
    private bool _seeded;

    internal readonly float BytesPerSecond => _rate;

    internal float Sample(long bytes, long nowMs)
    {

        if (!_seeded || bytes < _bytes)
        {
            _seeded = true;
            _bytes = bytes;
            _movedAt = nowMs;
            return _rate = 0f;
        }
        if (bytes > _bytes)
        {

            float seconds = MathF.Max((nowMs - _movedAt) / 1000f, 0.001f);
            _rate = (bytes - _bytes) / seconds;
            _bytes = bytes;
            _movedAt = nowMs;
            return _rate;
        }
        if (nowMs - _movedAt > StallMs) _rate = 0f;
        return _rate;
    }

        internal void Seed(float bytesPerSecond, long nowMs)
    {
        _seeded = true;
        _rate = bytesPerSecond;
        _movedAt = nowMs;
    }
}
