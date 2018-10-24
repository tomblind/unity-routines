//MIT License
//
//Copyright (c) 2018 Tom Blind
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Routines
{
	public class RoutineContext : IRoutineContext
	{
		private class NextFrameResumable : Resumable
		{
			public List<IResumer> newResumers = new List<IResumer>();
			public List<IResumer> resumers = new List<IResumer>();

			public override void Run(IResumer resumer)
			{
				newResumers.Add(resumer);
			}

			public void Resume()
			{
				foreach (var resumer in resumers)
				{
					resumer.Resume();
				}
				resumers.Clear();
			}

			public void Flush()
			{
				resumers.AddRange(newResumers);
				newResumers.Clear();
			}
		}

		private NextFrameResumable nextFrameResumable = new NextFrameResumable();
		private List<Routine> activeRoutines = new List<Routine>();

		public void Update()
		{
			for (var i = 0; i < activeRoutines.Count;)
			{
				var routine = activeRoutines[i];
				if (routine.IsDone)
				{
					Routine.Release(ref routine);
					activeRoutines.RemoveAt(i);
				}
				else
				{
					++i;
				}
			}

			nextFrameResumable.Resume();
		}

		public void LateUpdate()
		{
			nextFrameResumable.Flush();
		}

		public void OnDestroy()
		{
			StopAllRoutines();
		}

		public Routine.Handle RunRoutine(IEnumerator enumerator, System.Action onStop = null)
		{
			return RunRoutine(enumerator, null);
		}

		public Routine.Handle RunRoutine(IEnumerator enumerator, Object context, System.Action onStop = null)
		{
			var routine = Routine.Create();
			routine.Start(enumerator, context, null, onStop);
			activeRoutines.Add(routine);
			return routine.GetHandle();
		}

		public void StopAllRoutines()
		{
			for (var i = 0; i < activeRoutines.Count; ++i)
			{
				var routine = activeRoutines[i];
				Routine.Release(ref routine);
			}
			activeRoutines.Clear();
		}

		public IEnumerator WaitForNextFrame()
		{
			return nextFrameResumable;
		}

		public IEnumerator WaitForSeconds(float seconds)
		{
			while (seconds > 0.0f)
			{
				yield return nextFrameResumable;
				seconds -= Time.deltaTime;
			}
		}

		public IEnumerator WaitUntil(System.Func<bool> condition)
		{
			while (!condition())
			{
				yield return nextFrameResumable;
			}
		}

		public IEnumerator WaitForAsyncOperation(AsyncOperation asyncOperation, System.Action<float> onProgress = null)
		{
			var lastProgress = (onProgress != null) ? asyncOperation.progress : float.MaxValue;
			while (!asyncOperation.isDone)
			{
				yield return nextFrameResumable;

				if (asyncOperation.progress > lastProgress)
				{
					lastProgress = asyncOperation.progress;
					onProgress(lastProgress);
				}
			}
		}

		public IEnumerator WaitForCustomYieldInstruction(CustomYieldInstruction yieldInstruction)
		{
			while (yieldInstruction.keepWaiting)
			{
				yield return nextFrameResumable;
			}
		}
	}
}
