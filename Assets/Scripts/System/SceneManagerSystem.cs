using System;
using System.IO;
using System.Linq;
using Assets.Scripts.EditorState;
using Assets.Scripts.SceneState;
using Assets.Scripts.Utility;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zenject;
using Exception = System.Exception;
using USFB;

namespace Assets.Scripts.System
{
    public class SceneManagerSystem
    {
        // Dependencies
        private SceneManagerState sceneManagerState;
        private PathState pathState;
        private ISceneLoadSystem sceneLoadSystem;
        private ISceneSaveSystem sceneSaveSystem;
        private WorkspaceSaveSystem workspaceSaveSystem;
        private TypeScriptGenerationSystem typeScriptGenerationSystem;
        private SceneViewSystem sceneViewSystem;
        private MenuBarSystem menuBarSystem;

        [Inject]
        public void Construct(
            SceneManagerState sceneManagerState,
            PathState pathState,
            ISceneLoadSystem sceneLoadSystem,
            ISceneSaveSystem sceneSaveSystem,
            WorkspaceSaveSystem workspaceSaveSystem,
            TypeScriptGenerationSystem typeScriptGenerationSystem,
            SceneViewSystem sceneViewSystem,
            MenuBarSystem menuBarSystem)
        {
            this.sceneManagerState = sceneManagerState;
            this.pathState = pathState;
            this.sceneLoadSystem = sceneLoadSystem;
            this.sceneSaveSystem = sceneSaveSystem;
            this.workspaceSaveSystem = workspaceSaveSystem;
            this.typeScriptGenerationSystem = typeScriptGenerationSystem;
            this.sceneViewSystem = sceneViewSystem;
            this.menuBarSystem = menuBarSystem;

            CreateMenuBarItems();
        }

        public void DiscoverScenes()
        {
            // TODO: This should go through the asset manager

            var sceneDirectoryPaths = Directory.GetDirectories(pathState.ProjectPath, "*.dclscene", SearchOption.AllDirectories);

            foreach (var path in sceneDirectoryPaths)
            {
                SceneDirectoryState sceneDirectoryState = LoadSceneDirectoryState(path);
            }
        }

        /// <summary>
        /// Set any scene as current scene. Undefinded behaviour.
        /// </summary>
        public void SetFirstSceneAsCurrentScene()
        {
            SetCurrentScene(sceneManagerState.allSceneDirectoryStates.First().id);
        }

        /// <summary>
        /// Create a new scene and set it as current scene
        /// </summary>
        public void SetNewScneneAsCurrentScene()
        {
            SceneDirectoryState newScene = SceneDirectoryState.CreateNewSceneDirectoryState();
            sceneManagerState.AddSceneDirectoryState(newScene);
            SetCurrentScene(newScene.id);

        }

        /// <summary>
        /// Start an open file dialog and set the resulting scene as current scene.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetDialogAsCurrentScene()
        {
            string[] paths = StandaloneFileBrowser.OpenFolderPanel("Open Scene", pathState.ProjectPath, false);

            // check for canceled dialog
            if (paths.Length == 0)
            {
                return;
            }

            SetCurrentScene(paths[0]);
        }

        /// <summary>
        /// Set the given scene as current scene.
        /// </summary>
        /// <param name="path">The scene to set as current scene.</param>
        public void SetCurrentScene(string path)
        {
            SetCurrentScene(sceneManagerState.GetDirectoryState(path).id);
        }

        /// <summary>
        /// Set the given scene as current scene.
        /// </summary>
        /// <param name="id">The ID of the scene to set as current scene.</param>
        public void SetCurrentScene(Guid id)
        {
            sceneManagerState.SetCurrentSceneIndex(id);
            sceneViewSystem.SetUpCurrentScene();
        }

        /// <summary>
        /// Save the current scene to its directory.
        /// If it has no directory yet, this will result in save as.
        /// Note: Save as might actually not save, if the user does cancel the dialog.
        /// </summary>
        public void SaveCurrentScene()
        {
            SaveScene(GetCurrentDirectoryState());
        }

        /// <summary>
        /// Save the given scene to its directory.
        /// If it has no directory yet, this will result in save as.
        /// Note: Save as might actually not save, if the user does cancel the dialog.
        /// </summary>
        /// <param name="id">The ID of the scene to save.</param>
        public void SaveScene(Guid id)
        {
            SaveScene(sceneManagerState.GetDirectoryState(id));
        }

        /// <summary>
        /// Save the given scene to its directory.
        /// If it has no directory yet, this will result in save as.
        /// Note: Save as might actually not save, if the user does cancel the dialog.
        /// </summary>
        /// <param name="sceneDirectoryState">The SceneDirectoryState of the scene to save.</param>
        private void SaveScene(SceneDirectoryState sceneDirectoryState)
        {
            // Use save as if scene has no path yet
            if (sceneDirectoryState.directoryPath == null)
            {
                SaveSceneAs(sceneDirectoryState);
            }
            else
            {
                sceneSaveSystem.Save(sceneDirectoryState);
                workspaceSaveSystem.Save(); // TODO: Save the workspace under proper conditions.
                typeScriptGenerationSystem.GenerateTypeScript();
            }
        }

        /// <summary>
        /// Save the current scene to its directory with dialog.
        /// Note: This might actually not save, if the user does cancel the dialog.
        /// </summary>
        public void SaveCurrentSceneAs()
        {
            SaveSceneAs(GetCurrentDirectoryState());
        }

        /// <summary>
        /// Save the given scene to its directory with dialog.
        /// Note: This might actually not save, if the user does cancel the dialog.
        /// </summary>
        /// <param name="id">The SceneDirectoryState of the scene to save.</param>
        public void SaveSceneAs(Guid id)
        {
            SaveSceneAs(sceneManagerState.GetDirectoryState(id));
        }

        /// <summary>
        /// Save the given scene to its directory with dialog.
        /// Note: This might actually not save, if the user does cancel the dialog.
        /// </summary>
        /// <param name="sceneDirectoryState">The SceneDirectoryState of the scene to save.</param>
        private void SaveSceneAs(SceneDirectoryState sceneDirectoryState)
        {
            string oldPath = sceneDirectoryState.directoryPath;

            string oldContainingDirectoryPath;
            string oldName;
            if (oldPath == null)
            {
                oldContainingDirectoryPath = pathState.ProjectPath;
                oldName = "New Scene";
            }
            else
            {
                oldPath = Path.GetFullPath(oldPath); //normalize path format (e.g. turn '/' into '\\')
                oldContainingDirectoryPath = oldPath.Substring(0, oldPath.LastIndexOf(Path.DirectorySeparatorChar));
                oldName = oldPath.Substring(oldPath.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                oldName = oldName.Substring(0, oldName.LastIndexOf('.'));
            }

            string newPath = StandaloneFileBrowser.SaveFilePanel("Save Scene", oldContainingDirectoryPath, oldName, "dclscene");

            // check for canceled dialog
            if (newPath == "")
            {
                return;
            }

            //remove any potential scene that will be overridden
            if (sceneManagerState.TryGetDirectoryState(newPath, out SceneDirectoryState sceneDirectoryStateToOverride))
            {
                sceneManagerState.RemoveSceneDirectoryState(sceneDirectoryStateToOverride);
            }

            sceneDirectoryState.directoryPath = newPath;
            SaveScene(sceneDirectoryState);

            if (oldPath != null)
            {
                LoadSceneDirectoryState(oldPath);
            }
        }

        [CanBeNull]
        public DclScene GetCurrentScene()
        {
            //return null if no scene is open
            if (sceneManagerState.currentSceneIndex == Guid.Empty)
            {
                return null;
            }

            return GetScene(sceneManagerState.currentSceneIndex);
        }

        [NotNull]
        public DclScene GetScene(Guid id)
        {
            var sceneDirectoryState = sceneManagerState.GetDirectoryState(id);

            if (!sceneDirectoryState.IsSceneOpened())
            {
                sceneLoadSystem.Load(sceneDirectoryState);
            }

            return sceneDirectoryState.currentScene!;
        }

        private struct SceneFileContents
        {
            public Guid id;
            public string relativePath;
            public JObject settings;
        }

        /// <summary>
        /// Load a Scene from the file system and add it to the loaded SceneDirectoryStates in SceneManagerState.
        /// </summary>
        /// <param name="path">The directory to load the scene from.</param>
        /// <returns>The new loaded scene.</returns>
        private SceneDirectoryState LoadSceneDirectoryState(string path)
        {
            var sceneFileJson = Path.Combine(path, "scene.json");

            SceneFileContents sceneFileContents;
            try
            {
                sceneFileContents = JsonConvert.DeserializeObject<SceneFileContents>(sceneFileJson);
            }
            catch (Exception)
            {
                sceneFileContents = new SceneFileContents
                {
                    id = Guid.Empty,
                    relativePath = path,
                    settings = new JObject()
                };
            }

            if (sceneFileContents.id == Guid.Empty)
            {
                sceneFileContents.id = Guid.NewGuid();
            }

            SceneDirectoryState sceneDirectoryState = new SceneDirectoryState(sceneFileContents.relativePath, sceneFileContents.id);
            sceneManagerState.AddSceneDirectoryState(sceneDirectoryState);
            return sceneDirectoryState;
        }

        public SceneDirectoryState GetCurrentDirectoryState()
        {
            return sceneManagerState.GetCurrentDirectoryState();
        }

        private void CreateMenuBarItems()
        {
            menuBarSystem.AddMenuItem("File/New Scene", SetNewScneneAsCurrentScene);
            menuBarSystem.AddMenuItem("File/Open Scene", SetDialogAsCurrentScene);
            menuBarSystem.AddMenuItem("File/Save Scene", SaveCurrentScene);
            menuBarSystem.AddMenuItem("File/Save Scene As...", SaveCurrentSceneAs);
        }
    }
}