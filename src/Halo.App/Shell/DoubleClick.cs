using System;

namespace Halo.Shell;

internal struct DoubleClick
{

    internal const int SlopPx = 8;

    private long _lastAt;
    private int _lastX, _lastY;
    private bool _down;

    internal bool Step(bool down, long now, int x, int y, int windowMs)
    {
        bool press = down && !_down;
        _down = down;
        if (!press) return false;

        bool soon = _lastAt != 0 && now - _lastAt <= windowMs;
        bool near = Math.Abs(x - _lastX) <= SlopPx && Math.Abs(y - _lastY) <= SlopPx;
        _lastX = x;
        _lastY = y;
        if (soon && near)
        {

            _lastAt = 0;
            return true;
        }
        _lastAt = now;
        return false;
    }
}
