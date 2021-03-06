﻿using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace QSB.TransformSync
{
    public abstract class TransformSync : NetworkBehaviour
    {
        private const float SmoothTime = 0.1f;
        private bool _isInitialized;

        public Transform SyncedTransform { get; private set; }

        private bool _isSectorSetUp;
        private Vector3 _positionSmoothVelocity;
        private Quaternion _rotationSmoothVelocity;

        protected virtual void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_isInitialized)
            {
                Reset();
            }
        }

        protected abstract Transform InitLocalTransform();
        protected abstract Transform InitRemoteTransform();
        protected abstract bool IsReady();

        protected void Init()
        {
            _isInitialized = true;
            Invoke(nameof(SetFirstSector), 1);

            SyncedTransform = hasAuthority ? InitLocalTransform() : InitRemoteTransform();
            if (!hasAuthority)
            {
                SyncedTransform.position = Locator.GetAstroObject(AstroObject.Name.Sun).transform.position;
            }
        }

        protected void Reset()
        {
            _isInitialized = false;
            _isSectorSetUp = false;
        }

        private void SetFirstSector()
        {
            _isSectorSetUp = true;
            SectorSync.Instance.SetSector(netId.Value, Locator.GetAstroObject(AstroObject.Name.TimberHearth).transform);
        }

        public void EnterSector(Sector sector)
        {
            SectorSync.Instance.SetSector(netId.Value, sector.GetName());
        }

        private void Update()
        {
            if (!_isInitialized && IsReady())
            {
                Init();
            }
            else if (_isInitialized && !IsReady())
            {
                Reset();
            }

            if (!SyncedTransform || !_isSectorSetUp || !_isInitialized)
            {
                return;
            }

            var sectorTransform = SectorSync.Instance.GetSector(netId.Value);

            if (hasAuthority)
            {
                transform.position = sectorTransform.InverseTransformPoint(SyncedTransform.position);
                transform.rotation = sectorTransform.InverseTransformRotation(SyncedTransform.rotation);
            }
            else
            {
                if (SyncedTransform.position == Vector3.zero)
                {
                    SyncedTransform.position = Locator.GetAstroObject(AstroObject.Name.Sun).transform.position;
                }
                else
                {
                    SyncedTransform.parent = sectorTransform;

                    SyncedTransform.localPosition = Vector3.SmoothDamp(SyncedTransform.localPosition, transform.position, ref _positionSmoothVelocity, SmoothTime);
                    SyncedTransform.localRotation = QuaternionHelper.SmoothDamp(SyncedTransform.localRotation, transform.rotation, ref _rotationSmoothVelocity, Time.deltaTime);
                }
            }
        }
    }
}
