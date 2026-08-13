using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DarkFlare
{
    public sealed class ResourceRegenerationSystem : AbstractSystem
    {
        public const float TickInterval = 0.25f;
        public const float BaseManaRegenerationRate = 0.05f;

        CancellationTokenSource _loopCancellation;

        protected override void OnInit()
        {
            _loopCancellation = new CancellationTokenSource();
            RegenerationLoop(_loopCancellation.Token).Forget();
        }

        protected override void OnDeinit()
        {
            _loopCancellation?.Cancel();
            _loopCancellation?.Dispose();
            _loopCancellation = null;
        }

        public void AdvanceRegeneration(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f || this.GetSystem<GameplayPauseSystem>().IsPaused)
            {
                return;
            }

            RegenerateTeam(ActorTeam.Player, elapsedSeconds);
            RegenerateTeam(ActorTeam.Monster, elapsedSeconds);
        }

        void RegenerateTeam(ActorTeam team, float elapsedSeconds)
        {
            IReadOnlyList<CombatActor> actors = this.GetModel<CombatModel>().GetActorsByTeam(team);
            CombatSystem combatSystem = this.GetSystem<CombatSystem>();

            for (int i = 0; i < actors.Count; i++)
            {
                CombatActor actor = actors[i];

                if (actor == null || !actor.isActiveAndEnabled || !actor.IsAlive)
                {
                    continue;
                }

                float healthRegeneration = Math.Max(0f, actor.Stats.GetValue(StatIds.HealthRegeneration));
                float manaRegeneration = CalculateManaRegenerationPerSecond(
                    actor.MaxMana,
                    actor.Stats.GetValue(StatIds.ManaRegeneration));
                combatSystem.ApplyHealthRegeneration(actor, healthRegeneration * elapsedSeconds);
                combatSystem.RestoreMana(
                    actor,
                    manaRegeneration * elapsedSeconds,
                    ActorResourceChangeReason.Regeneration);
            }
        }

        public static float CalculateManaRegenerationPerSecond(
            float maxMana,
            float flatManaRegeneration)
        {
            return Math.Max(0f, maxMana) * BaseManaRegenerationRate
                + Math.Max(0f, flatManaRegeneration);
        }

        async UniTaskVoid RegenerationLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool cancelled = await UniTask.Delay(
                        TimeSpan.FromSeconds(TickInterval),
                        cancellationToken: token)
                    .SuppressCancellationThrow();

                if (cancelled)
                {
                    return;
                }

                AdvanceRegeneration(TickInterval);
            }
        }
    }
}
