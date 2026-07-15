using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    [DisallowMultipleComponent]
    public class CombatActor : MonoBehaviour, IController
    {
        [SerializeField]
        string _actorId = string.Empty;

        [SerializeField]
        ActorTeam _team;

        [SerializeField]
        float _maxHealth = 100f;

        [SerializeField]
        bool _hideVisualOnDeath = true;

        readonly List<Collider2D> _colliders = new List<Collider2D>();
        readonly List<ModifierInstance> _modifiers = new List<ModifierInstance>();

        Rigidbody2D _rigidbody;
        SpriteRenderer _renderer;
        StatBlock _stats = new StatBlock();
        TagSet _tags = TagSet.Empty;
        float _currentHealth;
        bool _isAlive;

        public string ActorId => string.IsNullOrWhiteSpace(_actorId) ? name : _actorId;

        public ActorTeam Team => _team;

        public float MaxHealth => _maxHealth;

        public float CurrentHealth => _currentHealth;

        public bool IsAlive => _isAlive;

        public StatBlock Stats => _stats;

        public IReadOnlyList<ModifierInstance> Modifiers => _modifiers;

        public TagSet Tags => _tags;

        public IArchitecture GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        public void Configure(string actorId, ActorTeam team, float maxHealth, StatBlock stats, TagSet tags)
        {
            _actorId = actorId;
            _team = team;
            _maxHealth = Mathf.Max(1f, maxHealth);
            _stats = stats != null ? stats.Clone() : new StatBlock();
            _stats.SetValue(StatIds.MaxHealth, _maxHealth);
            _tags = tags ?? TagSet.Empty;
            _currentHealth = _maxHealth;

            if (!_isAlive)
            {
                Revive(transform.position);
            }
        }

        public void ConfigureFromCharacter(CharacterDefinition definition, ActorTeam team)
        {
            Configure(definition.Id, team, definition.MaxHealth, definition.CreateStats(), TagSet.Empty);
        }

        public bool ReceiveDamage(DamageResult result)
        {
            if (!_isAlive || !result.IsHit)
            {
                return false;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - result.TotalDamage);

            if (_currentHealth <= 0f)
            {
                Die();
                return true;
            }

            return false;
        }

        public void SetModifiers(IEnumerable<ModifierInstance> modifiers)
        {
            _modifiers.Clear();

            if (modifiers != null)
            {
                _modifiers.AddRange(modifiers);
            }
        }

        public void Revive(Vector3 position)
        {
            CacheComponents();
            transform.position = position;
            _currentHealth = _maxHealth;
            _isAlive = true;
            SetPresentationEnabled(true);
        }

        void Awake()
        {
            CacheComponents();
            _currentHealth = _maxHealth;
            _isAlive = true;
        }

        void OnEnable()
        {
            this.SendCommand(new RegisterActorCommand(this));
        }

        void OnDisable()
        {
            this.SendCommand(new UnregisterActorCommand(this));
        }

        void OnValidate()
        {
            CacheComponents();
            _maxHealth = Mathf.Max(1f, _maxHealth);
        }

        void CacheComponents()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _renderer = GetComponent<SpriteRenderer>();
            _colliders.Clear();
            GetComponents(_colliders);
        }

        void Die()
        {
            if (!_isAlive)
            {
                return;
            }

            _isAlive = false;

            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector2.zero;
            }

            if (_hideVisualOnDeath)
            {
                SetPresentationEnabled(false);
            }
        }

        void SetPresentationEnabled(bool enabled)
        {
            for (int i = 0; i < _colliders.Count; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = enabled;
                }
            }

            if (_renderer != null)
            {
                _renderer.enabled = enabled;
            }
        }
    }
}
