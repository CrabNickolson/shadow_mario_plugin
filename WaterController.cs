using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LibSM64;

namespace ShadowMario;

internal class WaterController : MonoBehaviour
{
    public SM64Mario m_mario;
    public bool m_isGas;
    public float m_height;
    public bool m_setToOceanHeight;

    private float m_currentHeight = float.MinValue;

    private void Awake()
    {
        if (m_setToOceanHeight)
        {
            if (MiMissionHandler.bInstanceExists && MiMissionHandler.instance.currentLocationData.name.Contains("furnace"))
            {
                m_height = -5;
            }
            else
            {
                var goOcean = FindOcean();
                if (goOcean != null)
                    m_height = goOcean.transform.position.y;
            }

        }
    }

    private void Update()
    {
        //if (m_currentHeight != m_height) // would be more efficient but mario may have reset
        {
            m_currentHeight = m_height;
            if (m_isGas)
                m_mario.setGasLevel(m_currentHeight);
            else
                m_mario.setWaterLevel(m_currentHeight);
        }
    }

    public static GameObject FindOcean()
    {
        return GameObject.Find("MiOcean_00");
    }
}
