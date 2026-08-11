using System.Collections.Generic;
using UnityEngine;

namespace DarkFlare
{
    public readonly struct CombatResourceSnapshot
    {
        public float CurrentHealth { get; }

        public float MaxHealth { get; }

        public float CurrentMana { get; }

        public float MaxMana { get; }

        public CombatResourceSnapshot(
            float currentHealth,
            float maxHealth,
            float currentMana,
            float maxMana)
        {
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            CurrentMana = currentMana;
            MaxMana = maxMana;
        }
    }

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
        StatBlock _baseStats = new StatBlock();
        StatBlock _stats = new StatBlock();
        TagSet _tags = TagSet.Empty;
        float _currentHealth;
        float _currentMana;
        bool _isAlive;

        public string ActorId => string.IsNullOrWhiteSpace(_actorId) ? name : _actorId;

        public ActorTeam Team => _team;

        public float MaxHealth => GetEffectiveMaxHealth();

        public float CurrentHealth => _currentHealth;

        public float MaxMana => Mathf.Max(0f, _stats.GetValue(StatIds.Mana));

        public float CurrentMana => _currentMana;

        public CombatResourceSnapshot Resources => new CombatResourceSnapshot(
            CurrentHealth,
            MaxHealth,
            CurrentMana,
            MaxMana);

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
            _baseStats = stats != null ? stats.Clone() : new StatBlock();
            _baseStats.SetValue(StatIds.MaxHealth, _maxHealth);
            RebuildStats();
            _tags = CombatTagResolver.ResolveActorTags(team, tags);
            _currentHealth = MaxHealth;
            _currentMana = MaxMana;

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
            if (!_isAlive || result == null || !result.DidDealDamage)
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

        public float ReceiveHealing(float amount)
        {
            if (!_isAlive || amount <= 0f)
            {
                return 0f;
            }

            float previousHealth = _currentHealth;
            _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
            return _currentHealth - previousHealth;
        }

        public bool CanSpendMana(float amount)
        {
            return _isAlive && amount >= 0f && _currentMana + 0.0001f >= amount;
        }

        public bool TrySpendMana(float amount)
        {
            if (!CanSpendMana(amount))
            {
                return false;
            }

            _currentMana = Mathf.Max(0f, _currentMana - amount);
            return true;
        }

        public float ReceiveMana(float amount)
        {
            if (!_isAlive || amount <= 0f)
            {
                return 0f;
            }

            float previousMana = _currentMana;
            _currentMana = Mathf.Min(MaxMana, _currentMana + amount);
            return _currentMana - previousMana;
        }

        public void SetModifiers(IEnumerable<ModifierInstance> modifiers)
        {
            float previousMaxHealth = MaxHealth;
            float healthRatio = previousMaxHealth > 0f
                ? Mathf.Clamp01(_currentHealth / previousMaxHealth)
                : 1f;
            float previousMaxMana = MaxMana;
            float manaRatio = previousMaxMana > 0f
                ? Mathf.Clamp01(_currentMana / previousMaxMana)
                : 1f;
            _modifiers.Clear();

            if (modifiers != null)
            {
                _modifiers.AddRange(modifiers);
            }

            RebuildStats();
            _currentHealth = MaxHealth * healthRatio;
            _currentMana = MaxMana <= 0f
                ? 0f
                : previousMaxMana > 0f
                    ? MaxMana * manaRatio
                    : MaxMana;
        }

        public void Revive(Vector3 position)
        {
            CacheComponents();
            transform.position = position;
            _currentHealth = MaxHealth;
            _currentMana = MaxMana;
            _isAlive = true;
            SetPresentationEnabled(true);
        }

        void Awake()
        {
            CacheComponents();
            _currentHealth = MaxHealth;
            _currentMana = MaxMana;
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

        void RebuildStats()
        {
            _stats = CombatStatResolver.Build(_baseStats, _modifiers);
        }

        float GetEffectiveMaxHealth()
        {
            if (_baseStats.GetValue(StatIds.MaxHealth) <= 0f)
            {
                return Mathf.Max(1f, _maxHealth);
            }

            return Mathf.Max(1f, _stats.GetValue(StatIds.MaxHealth));
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

            SetCollidersEnabled(false);

            if (_hideVisualOnDeath && _renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        void SetPresentationEnabled(bool enabled)
        {
            SetCollidersEnabled(enabled);

            if (_renderer != null)
            {
                _renderer.enabled = enabled;
            }
        }

        void SetCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < _colliders.Count; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = enabled;
                }
            }
        }
    }
}
