using System;
using System.Collections.Generic;
using Assets.Scripts.Visuals.UiHandler;
using JetBrains.Annotations;
using UnityEngine;

namespace Assets.Scripts.Visuals.UiBuilder
{
    public class PanelAtom : Atom
    {
        public new class Data : Atom.Data
        {
            public PanelHandler.LayoutDirection layoutDirection = PanelHandler.LayoutDirection.Vertical;

            public AtomDataList childDates = new AtomDataList();

            public override bool Equals(Atom.Data other)
            {
                if (!(other is PanelAtom.Data otherPanel))
                {
                    return false;
                }

                if (childDates.Count != otherPanel.childDates.Count)
                {
                    return false;
                }

                for (var i = 0; i < childDates.Count; i++)
                {
                    if (!childDates[i].Equals(otherPanel.childDates[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Data data;

        public List<Atom> childAtoms = new List<Atom>();

        public override bool Update(Atom.Data newData, int newPosition)
        {
            UiBuilder.Stats.atomsUpdatedCount++;

            var hasChanged = false;
            var newPanelData = (Data) newData;

            if (gameObject == null)
            {
                gameObject = MakeNewAtomGameObject();
                hasChanged = true;
            }

            var newHeight = -1;

            if (!newPanelData.Equals(data))
            {
                UpdateData(newPanelData, out var changed, out newHeight);

                hasChanged = hasChanged || changed;
            }

            if (newPosition != gameObject.position || (newHeight > 0 && newHeight != gameObject.height))
            {
                UpdatePositionAndSize(newPosition, newHeight);
                hasChanged = true;
            }

            return hasChanged;
        }

        public override void Remove()
        {
            foreach (var childAtom in childAtoms)
            {
                childAtom.Remove();
            }

            base.Remove();
        }

        protected virtual AtomGameObject MakeNewAtomGameObject()
        {
            var atomObject = uiBuilder.GetAtomObjectFromPool(UiBuilder.AtomType.Panel);
            atomObject.height = 40;
            atomObject.position = -1;
            return atomObject;
        }

        protected void UpdateData(Data newPanelData, out bool hasChanged, out int endHeight)
        {
            hasChanged = false;

            // make layout group
            gameObject.gameObject.GetComponent<PanelHandler>().SetLayoutDirection(newPanelData.layoutDirection);

            var lastPos = 0;

            var atomIndex = 0;
            var dataIndex = 0;
            while (dataIndex < newPanelData.childDates.Count)
            {
                var childData = newPanelData.childDates[dataIndex];

                // case 1: atom and data match
                if (atomIndex < childAtoms.Count && childAtoms[atomIndex].DoesDataTypeMatch(childData))
                {
                    // Update the atom to represent the new data
                    var changed = childAtoms[atomIndex].Update(childData, lastPos);

                    if (changed)
                    {
                        hasChanged = true;
                    }

                    childAtoms[atomIndex].gameObject.gameObject.transform.SetParent(
                        gameObject.gameObject.GetComponent<PanelHandler>().content.transform, false);

                    childAtoms[atomIndex].gameObject.gameObject.transform.localScale = Vector3.one;

                    lastPos += childAtoms[atomIndex].gameObject.height;
                    // advance both atom index and data index
                    atomIndex++;
                    dataIndex++;
                }
                // case 2: atom and data don't match but there are still some child atoms to search for a match
                else if (atomIndex < childAtoms.Count && !childAtoms[atomIndex].DoesDataTypeMatch(childData))
                {
                    // remove the current atom
                    childAtoms[atomIndex].Remove();
                    childAtoms.RemoveAt(atomIndex);
                }
                // case 3: there is no atom left to mach against
                else
                {
                    // create new atom and populate with data
                    var childAtom = MakeChildAtom(childData);

                    var changed = childAtom.Update(childData, lastPos);

                    if (changed)
                    {
                        hasChanged = true;
                    }

                    childAtom.gameObject.gameObject.transform.SetParent(
                        gameObject.gameObject.GetComponent<PanelHandler>().content.transform, false);

                    childAtom.gameObject.gameObject.transform.localScale = Vector3.one;

                    lastPos += childAtom.gameObject.height;

                    childAtoms.Add(childAtom);

                    atomIndex++;
                    dataIndex++;
                }
            }

            // case 4: data is fully integrated but there are still some atoms left
            while (atomIndex < childAtoms.Count)
            {
                // remove the remaining atoms
                childAtoms[atomIndex].Remove();
                childAtoms.RemoveAt(atomIndex);
            }

            endHeight = lastPos + totalBorderHeight;
        }

        protected virtual int totalBorderHeight { get; set; } = 0;

        private Atom MakeChildAtom(Atom.Data childData)
        {
            return childData switch
            {
                TitleAtom.Data _ => new TitleAtom(uiBuilder),
                TextAtom.Data _ => new TextAtom(uiBuilder),
                SpacerAtom.Data _ => new SpacerAtom(uiBuilder),
                PanelWithBorderAtom.Data _ => new PanelWithBorderAtom(uiBuilder),
                PanelAtom.Data _ => new PanelAtom(uiBuilder),
                PanelHeaderAtom.Data _ => new PanelHeaderAtom(uiBuilder),
                HierarchyItemAtom.Data _ => new HierarchyItemAtom(uiBuilder),
                StringPropertyAtom.Data _ => new StringPropertyAtom(uiBuilder),
                NumberPropertyAtom.Data _ => new NumberPropertyAtom(uiBuilder),
                BooleanPropertyAtom.Data _ => new BooleanPropertyAtom(uiBuilder),
                Vector3PropertyAtom.Data _ => new Vector3PropertyAtom(uiBuilder),
                ContextMenuTextAtom.Data _ => new ContextMenuTextAtom(uiBuilder),
                ContextSubmenuAtom.Data _ => new ContextSubmenuAtom(uiBuilder),
                ContextMenuSpacerAtom.Data _ => new ContextMenuSpacerAtom(uiBuilder),
                _ => throw new ArgumentException()
            };
        }

        public PanelAtom(UiBuilder uiBuilder) : base(uiBuilder)
        {
        }
    }

    public static class PanelPanelHelper
    {
        public static PanelAtom.Data AddPanel(this PanelAtom.Data panelAtomData, [CanBeNull] AtomDataList childDates = null)
        {
            var data = new PanelAtom.Data
            {
                childDates = childDates ?? new AtomDataList()
            };

            panelAtomData.childDates.Add(data);
            return data;
        }
    }
}