using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterController))]
    public sealed class BossTelegraphRenderer : MonoBehaviour, IController
    {
        MonsterController _monster;
        LineRenderer _line;
        Material _material;

        public IArchitecture GetArchitecture() { return _monster != null ? _monster.GetArchitecture() : GameArchitectureProvider.RequireCurrent(); }

        void Awake()
        {
            _monster = GetComponent<MonsterController>();
            var child = new GameObject("Boss attack warning");
            child.transform.SetParent(transform, false);
            _line = child.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) { shader = Shader.Find("Sprites/Default"); }
            if (shader == null) { enabled = false; return; }
            _material = new Material(shader); _line.sharedMaterial = _material;
            _line.textureMode = LineTextureMode.Stretch; _line.widthMultiplier = .09f;
            _line.startColor = new Color(1f, .62f, .22f, .9f);
            _line.endColor = _line.startColor; _line.sortingOrder = 400;
            _line.useWorldSpace = true; _line.positionCount = 0;
        }

        void LateUpdate()
        {
            BossBehaviorRuntime runtime = _monster?.BossRuntime;
            BossBehaviorDefinition config = _monster?.Definition?.BossBehavior;
            if (runtime == null || config == null || !runtime.IsAuthorized || _line == null)
            { Clear(); return; }
            if (runtime.Action == BossActionPhase.SwipeWarning) { DrawSwipe(runtime, config); }
            else if (runtime.Action == BossActionPhase.ChargeWarning) { DrawCharge(runtime, config); }
            else { Clear(); }
        }

        void DrawSwipe(BossBehaviorRuntime runtime, BossBehaviorDefinition config)
        {
            const int Steps = 20;
            Vector2 center = transform.position;
            float direction = Mathf.Atan2(runtime.Facing.y, runtime.Facing.x);
            float half = config.SwipeAngle * .5f * Mathf.Deg2Rad;
            _line.positionCount = Steps + 3;
            _line.SetPosition(0, center);
            for (int i = 0; i <= Steps; i++)
            {
                float angle = direction - half + i * half * 2 / Steps;
                _line.SetPosition(i + 1, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * config.SwipeRadius);
            }
            _line.SetPosition(Steps + 2, center);
        }

        void DrawCharge(BossBehaviorRuntime runtime, BossBehaviorDefinition config)
        {
            Vector2 origin = transform.position;
            Vector2 facing = runtime.Facing;
            Vector2 side = new Vector2(-facing.y, facing.x) * config.ChargeHitRadius;
            Vector2 end = origin + facing * config.ChargeDistance;
            _line.positionCount = 5;
            _line.SetPosition(0, origin + side);
            _line.SetPosition(1, end + side);
            _line.SetPosition(2, end - side);
            _line.SetPosition(3, origin - side);
            _line.SetPosition(4, origin + side);
        }

        void Clear()
        {
            if (_line != null && _line.positionCount != 0) { _line.positionCount = 0; }
        }

        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            if (_material != null) { Destroy(_material); }
        }
    }
}
