using System.Collections.Generic;
using System.Linq;

namespace DarkFlare
{
    public sealed class ChangeStatusesCommand : AbstractCommand<StatusOperation>
    {
        readonly StatusTargetId _target;
        readonly StatusMutation[] _mutations;
        readonly long? _expectedVersion;

        public ChangeStatusesCommand(StatusTargetId target, IEnumerable<StatusMutation> mutations, long? expectedVersion = null)
        {
            _target = target;
            _mutations = mutations?.Take(StatusMutationResolver.MaxTargetLayers + 1).ToArray();
            _expectedVersion = expectedVersion;
        }

        protected override StatusOperation OnExecute()
        {
            return this.GetSystem<StatusSystem>().Store.Execute(_target, _mutations, _expectedVersion);
        }
    }
}
