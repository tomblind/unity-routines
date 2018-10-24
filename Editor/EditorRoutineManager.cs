using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;

namespace Routines
{
    public class EditorRoutineManager : IRoutineContext
    {
        private static EditorRoutineManager instance;
        private static object _lock = new object();

        private RoutineContext context;

        private EditorRoutineManager()
        {
            this.context = new RoutineContext();
        }

        public static EditorRoutineManager Instance
        {
            get
            {
                lock(_lock)
                {
                    if (instance == null)
                    {
                        instance = new EditorRoutineManager();
                        instance.Start();
                    }

                    return instance;
                }
            }
        }

        public Routine.Handle RunRoutine(IEnumerator enumerator, System.Action onStop = null)
		{
            return context.RunRoutine(enumerator, null, onStop);
		}

        public IEnumerator WaitForNextFrame()
		{
            return context.WaitForNextFrame();
		}

		public IEnumerator WaitForSeconds(float seconds)
		{
            return context.WaitForSeconds(seconds);
		}

		public IEnumerator WaitUntilCondition(System.Func<bool> condition)
		{
            return context.WaitUntilCondition(condition);
		}

		public IEnumerator WaitForAsyncOperation(AsyncOperation asyncOperation, OnProgressDelegate onProgress = null)
		{
            return context.WaitForAsyncOperation(asyncOperation, onProgress);
		}

		public IEnumerator WaitForCustomYieldInstruction(CustomYieldInstruction yieldInstruction)
		{
            return context.WaitForCustomYieldInstruction(yieldInstruction);
		}

        private void Start()
        {
            Assert.IsNotNull(context);

            EditorApplication.update += Step;
            EditorApplication.playModeStateChanged += OnPlayModeStateChange;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private void OnBeforeAssemblyReload()
        {
            Stop();
        }

        private void OnPlayModeStateChange(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.ExitingEditMode)
            {
                Stop();
            }
        }

        private void Stop()
        {
            if (context == null) 
            {
                return;
            }

            EditorApplication.update -= Step;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChange;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            context.OnDestroy();

            lock (_lock)
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            context = null;
        }

        private void Step()
        {
            Assert.IsNotNull(context);

            context.Update();
            context.LateUpdate();
        }
    }
}
