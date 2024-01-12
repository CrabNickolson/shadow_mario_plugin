using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mimimi.Cam;
using LibSM64;
using PirateBase;

namespace ShadowMario;

internal class MarioCam : MiCam
{
    public float m_lerpSpeed = 2;
    public float m_spinSpeed = 80;
    public float m_zoomSpeed = 2;

    private SM64Mario m_mario;
    private Transform m_parent;
    private float m_zoom;

    private float m_originalFOV;
    private float m_originalΝearPlane;
    private float m_originalFarPlane;

    private const float c_posXZOffset = 8.5f;

    public static MarioCam CreateCam()
    {
        var camParentGo = new GameObject("sm64_cam_parent");
        camParentGo.transform.SetParent(SaveLoadSceneManager.transGetRoot());

        var camGo = new GameObject("sm64_cam");
        camGo.transform.SetParent(camParentGo.transform);
        var cam = camGo.AddComponent<MarioCam>();
        cam.m_parent = camParentGo.transform;
        cam.transform.localPosition = new Vector3(c_posXZOffset, c_posXZOffset, -c_posXZOffset);
        cam.transform.localRotation = Quaternion.Euler(25, -40, 0);

        return cam;
    }

    public override void onStartUse()
    {
        m_mario = MarioSceneHandler.instance.leaderMario.mario;
        m_zoom = 1;

        m_originalFOV = this.cam.fieldOfView;
        this.cam.fieldOfView = 60;
        m_originalΝearPlane = this.cam.nearClipPlane;
        this.cam.nearClipPlane = 0.1f;
        m_originalFarPlane = this.cam.farClipPlane;
        this.cam.farClipPlane = 400;

        m_parent.position = m_mario.position.ToIL2CPP();

        this.enabled = true;
    }

    public override void onEndUse()
    {
        m_mario = null;

        this.cam.fieldOfView = m_originalFOV;
        this.cam.nearClipPlane = m_originalΝearPlane;
        this.cam.farClipPlane = m_originalFarPlane;

        this.enabled = false;
    }

    private void Update()
    {
        if (ModUpdate.shouldSkipUpdate)
            return;

        m_parent.position = Vector3.Lerp(m_parent.position, m_mario.position.ToIL2CPP(), m_lerpSpeed * Time.deltaTime);

        float spin = Input.GetAxisRaw("Joy1Axis3");
        transform.RotateAround(m_parent.position, Vector3.up, -spin * m_spinSpeed * Time.deltaTime);

        float zoom = Input.GetAxisRaw("Joy1Axis2");
        m_zoom = Mathf.Clamp(m_zoom + zoom * m_zoomSpeed * Time.deltaTime, 0.2f, 2f);

    }
}
