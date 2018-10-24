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
	public class RoutineManager : MonoBehaviour, IRoutineContext
	{
		private RoutineContext context = new RoutineContext();

		protected void Update()
		{
			context.Update();
		}

		protected void LateUpdate()
		{
			context.LateUpdate();
		}

		public void OnDestroy()
		{
			context.StopAllRoutines();
		}

		public Routine.Handle RunRoutine(IEnumerator enumerator, System.Action onStop = null)
		{
			return context.RunRoutine(enumerator, this, onStop);
		}

		public void StopAllRoutines()
		{
			context.StopAllRoutines();
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
	}
}
