using UnityEngine;
using System.Linq;

namespace LibSM64
{
    public class SM64DynamicTerrain : MonoBehaviour
    {
        SM64TerrainType terrainType = SM64TerrainType.Grass;
        SM64SurfaceType surfaceType = SM64SurfaceType.Default;

        public SM64TerrainType TerrainType { get { return terrainType; } }
        public SM64SurfaceType SurfaceType { get { return surfaceType; } }

        Vector3 _position;
        Vector3 _lastPosition;
        Quaternion _rotation;
        Quaternion _lastRotation;
        bool _changedLastFrame;
        uint _surfaceObjectId;

        private Transform m_cachedTransform;
        public Transform trans
        {
            get
            {
                if (m_cachedTransform == null)
                {
                    m_cachedTransform = base.transform;
                }

                return m_cachedTransform;
            }
        }

        public Vector3 position { get { return _position; } }
        public Vector3 lastPosition { get { return _lastPosition; } }
        public Quaternion rotation { get { return _rotation; } }
        public Quaternion lastRotation { get { return _lastRotation; } }

        void OnEnable()
        {
            SM64Context.RegisterSurfaceObject(this);

            _position = trans.position;
            _rotation = trans.rotation;
            _lastPosition = _position;
            _lastRotation = _rotation;

            var mc = GetComponentInChildren<MeshCollider>();
            var surfaces = Utils.GetSurfacesForMesh(trans.lossyScale, mc.sharedMesh, surfaceType, terrainType);
            _surfaceObjectId = Interop.SurfaceObjectCreate(_position, _rotation, surfaces.ToArray());
        }

        void OnDisable()
        {
            if (Interop.isGlobalInit)
            {
                SM64Context.UnregisterSurfaceObject(this);
                Interop.SurfaceObjectDelete(_surfaceObjectId);
            }
        }

        internal void contextFixedUpdate()
        {
            _lastPosition = _position;
            _lastRotation = _rotation;

            bool changed = trans.hasChanged;
            if (changed || _changedLastFrame)
            {
                _position = trans.position;
                _rotation = trans.rotation;
                Interop.SurfaceObjectMove( _surfaceObjectId, _position, _rotation );
                _changedLastFrame = changed;
                trans.hasChanged = false;
            }
        }

        internal void contextUpdate()
        {
            /*float t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;

            trans.position = Vector3.LerpUnclamped( _lastPosition, _position, t );
            trans.rotation = Quaternion.SlerpUnclamped( _lastRotation, _rotation, t );*/
        }
    }
}