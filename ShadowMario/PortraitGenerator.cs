using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowMario;

internal static class PortraitGenerator
{
    public static Sprite Generate(Vector2Int _dimensions, LibSM64.SM64Mario _mario)
    {
        if (_mario == null || _mario.action != LibSM64.SM64MarioAction.IDLE)
            return null;

        var camGo = new GameObject("sm64Cam");
        Vector3 forward = Quaternion.Euler(0, _mario.faceAngle * Mathf.Rad2Deg, 0) * Vector3.forward;
        camGo.transform.position = _mario.transform.position + (forward * 2) + new Vector3(0, 1.1f, 0);
        camGo.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        var cam = camGo.AddComponent<Camera>();



        var rt = RenderTexture.GetTemporary(_dimensions.x, _dimensions.y, 16, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB);

        _mario.rendererObject.layer = (int)MiLayer.PlayerTrigger;
        cam.fieldOfView = 30;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.forceIntoRenderTexture = true;
        cam.targetTexture = rt;
        cam.cullingMask = 1 << (int)MiLayer.PlayerTrigger;
        cam.Render();



        var t = new Texture2D(_dimensions.x, _dimensions.y, TextureFormat.RGBA32, true);
        RenderTexture.active = rt;
        t.ReadPixels(new Rect(0, 0, _dimensions.x, _dimensions.y), 0, 0);
        t.Apply(true, true);

        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(camGo);
        _mario.rendererObject.layer = (int)MiLayer.Default;

        return Sprite.Create(t, new Rect(0, 0, _dimensions.x, _dimensions.y), new Vector2(0.5f, 0.5f));
    }
}
