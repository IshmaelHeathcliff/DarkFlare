using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public sealed class ApplicationInputModuleBinder : MonoBehaviour
    {
        [SerializeField]
        InputSystemUIInputModule _inputModule;

        ApplicationInputService _inputService;
        readonly List<InputActionReference> _actionReferences =
            new List<InputActionReference>();
        bool _enableInputModuleWhenBound;

        public bool IsBound { get; private set; }

        void Awake()
        {
            EnsureComponents();
            _enableInputModuleWhenBound = _inputModule.enabled;
        }

        void OnEnable()
        {
            ApplicationHost.InputReady += Bind;

            if (ApplicationHost.TryGetCurrent(out ApplicationHost host))
            {
                Bind(host.Input);
            }
        }

        void OnDisable()
        {
            ApplicationHost.InputReady -= Bind;
            Unbind();
        }

        void Bind(ApplicationInputService inputService)
        {
            if (inputService == null || inputService.IsClosed)
            {
                return;
            }

            if (ReferenceEquals(_inputService, inputService) && IsBound)
            {
                return;
            }

            Unbind();
            EnsureComponents();
            _inputService = inputService;
            _inputService.Closing += Unbind;
            _inputModule.actionsAsset = _inputService.ActionAsset;
            BindActionReferences(_inputService.ActionAsset);
            _inputModule.enabled = _enableInputModuleWhenBound;
            IsBound = ReferenceEquals(_inputModule.actionsAsset, _inputService.ActionAsset);

            if (!IsBound)
            {
                Debug.LogError(
                    "[ApplicationInputModuleBinder] UI Input Module 绑定运行时 Action Asset 失败",
                    this);
                Unbind();
            }
        }

        void Unbind()
        {
            if (_inputService != null)
            {
                _inputService.Closing -= Unbind;
                _inputService = null;
            }

            if (_inputModule != null)
            {
                _inputModule.enabled = false;
                _inputModule.point = null;
                _inputModule.move = null;
                _inputModule.submit = null;
                _inputModule.cancel = null;
                _inputModule.leftClick = null;
                _inputModule.rightClick = null;
                _inputModule.middleClick = null;
                _inputModule.scrollWheel = null;
                _inputModule.trackedDevicePosition = null;
                _inputModule.trackedDeviceOrientation = null;
                _inputModule.actionsAsset = null;
            }

            ReleaseActionReferences();
            IsBound = false;
        }

        void BindActionReferences(InputActionAsset actionAsset)
        {
            _inputModule.point = CreateActionReference(actionAsset, "Point");
            _inputModule.move = CreateActionReference(actionAsset, "Navigate");
            _inputModule.submit = CreateActionReference(actionAsset, "Submit");
            _inputModule.cancel = CreateActionReference(actionAsset, "Cancel");
            _inputModule.leftClick = CreateActionReference(actionAsset, "Click");
            _inputModule.rightClick = CreateActionReference(actionAsset, "RightClick");
            _inputModule.middleClick = CreateActionReference(actionAsset, "MiddleClick");
            _inputModule.scrollWheel = CreateActionReference(actionAsset, "ScrollWheel");
            _inputModule.trackedDevicePosition = CreateActionReference(
                actionAsset,
                "TrackedDevicePosition");
            _inputModule.trackedDeviceOrientation = CreateActionReference(
                actionAsset,
                "TrackedDeviceOrientation");
        }

        InputActionReference CreateActionReference(
            InputActionAsset actionAsset,
            string actionName)
        {
            InputAction action = actionAsset.FindAction(
                $"UI/{actionName}",
                true);
            InputActionReference reference = InputActionReference.Create(action);
            _actionReferences.Add(reference);
            return reference;
        }

        void ReleaseActionReferences()
        {
            for (int i = _actionReferences.Count - 1; i >= 0; i--)
            {
                if (_actionReferences[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(_actionReferences[i]);
                }
                else
                {
                    DestroyImmediate(_actionReferences[i]);
                }
            }

            _actionReferences.Clear();
        }

        void OnValidate()
        {
            EnsureComponents();
        }

        void EnsureComponents()
        {
            if (_inputModule != null)
            {
                return;
            }

            if (TryGetComponent(out InputSystemUIInputModule inputModule))
            {
                _inputModule = inputModule;
                return;
            }

            _inputModule = gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
