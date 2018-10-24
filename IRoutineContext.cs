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
		IEnumerator WaitUntilCondition(System.Func<bool> condition);
		IEnumerator WaitForAsyncOperation(AsyncOperation asyncOperation, OnProgressDelegate onProgress = null);
		IEnumerator WaitForCustomYieldInstruction(CustomYieldInstruction yieldInstruction);
	}

	public static class IRoutineContextExtensions
	{
		// Mimic Unity's interface
		public static IEnumerator WaitUntil(this IRoutineContext context, System.Func<bool> condition)
		{
			return context.WaitUntilCondition(condition);
		}
	}
}
