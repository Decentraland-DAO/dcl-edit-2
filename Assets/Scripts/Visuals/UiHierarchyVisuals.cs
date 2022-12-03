using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.EditorState;
using Assets.Scripts.Events;
using Assets.Scripts.SceneState;
using Assets.Scripts.System;
using Assets.Scripts.Visuals.UiHandler;
using UnityEngine;
using Zenject;

namespace Assets.Scripts.Visuals
{
    public class UiHierarchyVisuals : MonoBehaviour, ISetupSceneEventListeners
    {
#pragma warning disable CS0649 // Warning: Uninitialized filed. Serialized fields will be initialized by Unity

        [SerializeField]
        private GameObject _content;

#pragma warning restore CS0649

        void Update()
        {
            // Print UiBuilder Stats
            if (Input.GetKeyDown(KeyCode.L))
            {
                NewUiBuilder.NewUiBuilder.Stats.Dump();
            }

            if (Input.GetKeyDown(KeyCode.K))
            {
                dateIndex++;
                dateIndex %= datess.Count;
                UpdateVisuals();
            }
        }

        // Dependencies
        private EditorEvents _events;
        private UiBuilder.Factory _uiBuilderFactory;
        private NewUiBuilder.NewUiBuilder newUiBuilder;
        private SceneDirectoryState _sceneDirectoryState;
        private CommandSystem _commandSystem;
        private HierarchyChangeSystem _hierarchyChangeSystem;

        [Inject]
        private void Construct(EditorEvents events, UiBuilder.Factory uiBuilderFactory, NewUiBuilder.NewUiBuilder.Factory newUiBuilderFactory, SceneDirectoryState scene, CommandSystem commandSystem, HierarchyChangeSystem hierarchyChangeSystem)
        {
            _events = events;
            _uiBuilderFactory = uiBuilderFactory;
            newUiBuilder = newUiBuilderFactory.Create(_content);
            _sceneDirectoryState = scene;
            _commandSystem = commandSystem;
            _hierarchyChangeSystem = hierarchyChangeSystem;
        }

        public void SetupSceneEventListeners()
        {
            _events.onHierarchyChangedEvent += UpdateVisuals;
            _events.onSelectionChangedEvent += UpdateVisuals;
            UpdateVisuals();
        }

        private List<List<NewUiBuilder.Atom.Data>> datess = new List<List<NewUiBuilder.Atom.Data>>
        {
            new List<NewUiBuilder.Atom.Data>
            {
                new NewUiBuilder.TextAtom.Data {text = "a Test Text 1"},
                new NewUiBuilder.TitleAtom.Data {text = "a Test Text 2"},
                new NewUiBuilder.PanelAtom.Data
                {
                    childDates = new List<NewUiBuilder.Atom.Data>
                    {
                        new NewUiBuilder.TextAtom.Data {text = "a Test Text inner 3"},
                        new NewUiBuilder.TitleAtom.Data {text = "a Test Text inner 4"},
                    }
                },
                new NewUiBuilder.TitleAtom.Data {text = "a Test Text 5"},
                new NewUiBuilder.TextAtom.Data {text = "a Test Text 6"},
            },
            new List<NewUiBuilder.Atom.Data>
            {
                new NewUiBuilder.TextAtom.Data {text = "a Test Text 1"},
                new NewUiBuilder.TitleAtom.Data {text = "a Test Text 2"},
                new NewUiBuilder.TextAtom.Data {text = "a Test Text not inner 3"},
                new NewUiBuilder.TitleAtom.Data {text = "b Test Text not inner 4"},
                new NewUiBuilder.TitleAtom.Data {text = "b Test Text 5"},
                new NewUiBuilder.TextAtom.Data {text = "b Test Text 6"},
                new NewUiBuilder.TextAtom.Data {text = "b Test Text 7"},
            },
            new List<NewUiBuilder.Atom.Data>
            {
                new NewUiBuilder.TitleAtom.Data {text = "a Test Text 1"},
                new NewUiBuilder.TextAtom.Data {text = "Test Text 2"},
                new NewUiBuilder.PanelAtom.Data
                {
                    childDates = new List<NewUiBuilder.Atom.Data>
                    {
                        new NewUiBuilder.TextAtom.Data {text = "Test Text inner 3"},
                        new NewUiBuilder.TitleAtom.Data {text = "b Test Text inner 4"},
                    }
                },
            },
        };

        private int dateIndex = 0;

        private void UpdateVisuals()
        {
            var dates = datess[dateIndex];
            newUiBuilder.Update(new NewUiBuilder.PanelAtom.Data {childDates = dates});
        }

        //private void UpdateVisuals()
        //{
        //    MakeHierarchyItemsRecursive(0, _sceneDirectoryState.CurrentScene!.EntitiesInSceneRoot);
        //
        //    uiBuilder.Spacer(300);
        //
        //    uiBuilder.ClearAndMake(_content);
        //}
        //
        //private void MakeHierarchyItemsRecursive(int level, IEnumerable<DclEntity> entities)
        //{
        //    foreach (var entity in entities)
        //    {
        //        var isPrimarySelection = _sceneDirectoryState.CurrentScene!.SelectionState.PrimarySelectedEntity == entity;
        //
        //        var isSecondarySelection = _sceneDirectoryState.CurrentScene!.SelectionState.SecondarySelectedEntities.Contains(entity);
        //
        //        var style =
        //            isPrimarySelection ?
        //                TextHandler.TextStyle.PrimarySelection :
        //                isSecondarySelection ?
        //                    TextHandler.TextStyle.SecondarySelection :
        //                    TextHandler.TextStyle.Normal;
        //
        //        var isExpanded = _hierarchyChangeSystem.IsExpanded(entity);
        //
        //        uiBuilder.HierarchyItem(entity.ShownName, level, entity.Children.Any(), isExpanded, style, new HierarchyItemHandler.UiHierarchyItemActions
        //        {
        //            OnArrowClick = () => { _hierarchyChangeSystem.ClickedOnEntityExpandArrow(entity); },
        //            OnNameClick = () => { _hierarchyChangeSystem.ClickedOnEntityInHierarchy(entity); }
        //        });
        //
        //        if (isExpanded)
        //        {
        //            MakeHierarchyItemsRecursive(level + 1, entity.Children);
        //        }
        //    }
        //}
    }
}