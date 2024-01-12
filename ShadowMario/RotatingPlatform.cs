using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowMario;

internal class RotatingPlatform : MonoBehaviour
{
    public float m_speed = 2;
    public Vector3 m_axis = Vector3.right;

    private void Update()
    {
        transform.Rotate(m_axis, m_speed * Time.deltaTime);
    }
}
