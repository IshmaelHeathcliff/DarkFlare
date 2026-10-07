namespace DarkFlare
{
    public static class SkillBoardPlayerIdentity
    {
        public static string GetProfileId(CombatActor actor)
        {
            if (actor == null || actor.Team != ActorTeam.Player) { return null; }
            PlayerController controller = actor.GetComponent<PlayerController>();
            return controller != null ? controller.Id.Value : actor.ActorId;
        }
    }
}
