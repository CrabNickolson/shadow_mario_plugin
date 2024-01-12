using System.Numerics;

namespace ShadowMario;

public struct ModBounds : System.IEquatable<ModBounds>
{
    private Vector3 m_center;
    private Vector3 m_extents;

    public Vector3 center
    {
        get => m_center;
        set => m_center = value;
    }

    public Vector3 size
    {
        get => m_extents * 2f;
        set => m_extents = value * 0.5f;
    }

    public Vector3 extents
    {
        get => m_extents;
        set => m_extents = value;
    }

    public Vector3 min
    {
        get => center - extents;
        set => SetMinMax(value, max);
    }

    public Vector3 max
    {
        get => center + extents;
        set => SetMinMax(min, value);
    }

    public ModBounds(Vector3 _center, Vector3 _size)
    {
        m_center = _center;
        m_extents = _size * 0.5f;
    }

    public ModBounds(UnityEngine.Vector3 _center, UnityEngine.Vector3 _size)
    {
        m_center = new Vector3(_center.x, _center.y, _center.z);
        m_extents = new Vector3(_size.x * 0.5f, _size.y * 0.5f, _size.z * 0.5f);
    }

    public ModBounds(UnityEngine.Bounds _bounds)
    {
        m_center = new Vector3(_bounds.center.x, _bounds.center.y, _bounds.center.z);
        m_extents = new Vector3(_bounds.extents.x, _bounds.extents.y, _bounds.extents.z);
    }

    public void SetMinMax(Vector3 _min, Vector3 _max)
    {
        extents = (_max - _min) * 0.5f;
        center = _min + extents;
    }

    public void Encapsulate(Vector3 _point)
    {
        SetMinMax(Vector3.Min(min, _point), Vector3.Max(max, _point));
    }

    public void Encapsulate(ModBounds _bounds)
    {
        Encapsulate(_bounds.center - _bounds.extents);
        Encapsulate(_bounds.center + _bounds.extents);
    }

    public void Expand(float _amount)
    {
        _amount *= 0.5f;
        extents += new Vector3(_amount, _amount, _amount);
    }

    public void Expand(Vector3 _amount)
    {
        extents += _amount * 0.5f;
    }

    public bool Intersects(ModBounds _bounds)
    {
        return min.X <= _bounds.max.X && max.X >= _bounds.min.X && min.Y <= _bounds.max.Y
            && max.Y >= _bounds.min.Y && min.Z <= _bounds.max.Z && max.Z >= _bounds.min.Z;
    }

    public override int GetHashCode()
    {
        return center.GetHashCode() ^ (extents.GetHashCode() << 2);
    }

    public override bool Equals(object _other)
    {
        if (!(_other is ModBounds))
        {
            return false;
        }

        return Equals((ModBounds)_other);
    }

    public bool Equals(ModBounds _other)
    {
        return center.Equals(_other.center) && extents.Equals(_other.extents);
    }

    public static bool operator ==(ModBounds _lhs, ModBounds _rhs)
    {
        return _lhs.center == _rhs.center && _lhs.extents == _rhs.extents;
    }

    public static bool operator !=(ModBounds _lhs, ModBounds _rhs)
    {
        return !(_lhs == _rhs);
    }
}