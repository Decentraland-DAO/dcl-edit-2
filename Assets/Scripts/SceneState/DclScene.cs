using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.SceneState
{
    public class DclScene
    {
        public string name = "New Scene";

        public Dictionary<Guid, DclEntity> AllEntities = new Dictionary<Guid, DclEntity>();

        public IEnumerable<DclEntity> EntitiesInSceneRoot =>
            AllEntities
                .Where(e => e.Value.Parent == null)
                .Select(e => e.Value);

        public DclEntity GetEntityFormId(Guid id)
        {
            if (id == Guid.Empty)
                return null;

            return AllEntities.TryGetValue(id, out var entity) ? entity : null;
        }

        // Other States

        public SelectionState SelectionState = new SelectionState();

        public CommandHistoryState CommandHistoryState = new CommandHistoryState();

        public DclComponent.DclComponentProperty GetPropertyFromIdentifier(DclPropertyIdentifier identifier)
        {
            return GetEntityFormId(identifier.Entity)
                    .GetComponentByName(identifier.Component)
                    .GetPropertyByName(identifier.Property);
        }
    }
}
