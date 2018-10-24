using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Routines
{
	public partial interface IRoutineContext
	{
		Routine.Handle RunRoutine(IEnumerator routine, System.Action onStop = null);
		IEnumerator WaitForNextFrame();
		IEnumerator WaitForSeconds(float seconds);
		IEnumerator WaitUntil(System.Func<bool> condition);
		IEnumerator WaitForAsyncOperation(AsyncOperation asyncOperation, System.Action<float> onProgress = null);
		IEnumerator WaitForCustomYieldInstruction(CustomYieldInstruction yieldInstruction);
	}
}
