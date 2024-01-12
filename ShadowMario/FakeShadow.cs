using BepInEx;
using Il2CppSystem.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem;
using UnityEngine;
using PirateBase;

namespace ShadowMario;

internal class FakeShadow : MonoBehaviour
{
    private float m_scale;
    private bool m_scaleByDistance;

    private Transform m_cachedTransform;
    public Transform trans
    {
        get
        {
            if (m_cachedTransform == null)
                m_cachedTransform = this.transform;
            return m_cachedTransform;
        }
    }

    public FakeShadow(System.IntPtr ptr) : base(ptr) { }

    public FakeShadow() : base(ClassInjector.DerivedConstructorPointer<FakeShadow>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public static FakeShadow Spawn(Transform _parent, float _scale = 1, bool _scaleByDistance = false, bool _isSquare = false)
    {
        var go = GameObject.Instantiate(MarioResources.LoadShadowPrefab(_isSquare));
        go.SetActive(false);
        go.transform.SetParent(_parent, false);
        go.transform.localScale = new Vector3(_scale, _scale, _scale);

        var shadow = go.AddComponent<FakeShadow>();
        shadow.m_scale = _scale;
        shadow.m_scaleByDistance = _scaleByDistance;
        shadow.UpdateShadow();

        go.SetActive(true);

        return shadow;
    }

    private void LateUpdate()
    {
        if (trans.hasChanged)
        {
            UpdateShadow();
            trans.hasChanged = false;
        }
    }

    [HideFromIl2Cpp]
    public void UpdateShadow()
    {
        if (trans.parent == null)
            return;
        
        const float rayOffset = 0.2f;

        if (Physics.Raycast(
            trans.parent.position + new Vector3(0, rayOffset, 0),
            Vector3.down,
            out RaycastHit hit,
            100,
            MarioStateSyncer.marioColliderMask,
            QueryTriggerInteraction.Ignore))
        {
            trans.position = hit.point + new Vector3(0, 0.05f, 0);
            trans.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            if (m_scaleByDistance)
            {
                float scale = Mathf.Lerp(m_scale, m_scale * 0.6f, (hit.distance - rayOffset) * 0.25f);
                trans.localScale = new Vector3(scale, scale, scale);
            }
        }
    }
}
