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
using UnityEngine.Assertions;

namespace Routines
{
	public class Routine
	{
		public delegate void ExceptionHandlerDelegate(System.Exception e, Object context = null);

		public interface IWrappedArray {}

		//Use to safely perform actions on a routine
		public struct Handle
		{
			private Routine routine;
			private ulong id;

			public bool IsDone
			{
				get
				{
					return (routine == null || id != routine.id || routine.IsDone);
				}
			}

            public Routine Get()
            {
                return IsDone ? null : routine;
            }

			public Handle(Routine routine, ulong id)
			{
				this.routine = routine;
				this.id = id;
			}

			public void Stop()
			{
				if (routine != null && id == routine.id)
				{
					routine.Stop();
				}
			}
		}

		private class WrappedArray : IWrappedArray
		{
			public IEnumerable yieldables;
			public bool isAny;
		}

		private class RoutineException: System.Exception
		{
			public RoutineException() {}
			public RoutineException(string message) : base(message) {}
			public RoutineException(string message, System.Exception inner) : base(message, inner) {}
		}

		private class Resumer : IResumer
		{
			public ulong steppingId;
			public Routine routine;
			public bool called;

			public bool IsAlive
			{
				get
				{
					return (steppingId != noId && steppingId == routine.id);
				}
			}

			public void Resume(object result)
			{
				Assert.IsFalse(called);
				called = true;
				if (steppingId == routine.id)
				{
					routine.returnValue = result;
					routine.Finish(null);
				}
				Release();
			}

			public void Throw(System.Exception error)
			{
				Assert.IsFalse(called);
				called = true;
				if (steppingId == routine.id)
				{
					routine.Finish(routine.CreateException(error.Message, error));
				}
				Release();
			}

			public void Release()
			{
				resumerPool.Push(this);
			}

			public void Reset()
			{
				steppingId = noId;
				routine = null;
				called = false;
			}
		}

		private class ImmediateResumable : Resumable
		{
			public override void Run(IResumer resumer)
			{
				resumer.Resume();
			}
		}

		private const ulong noId = ulong.MaxValue;
		private const int maxIterations = 999999; //Maximum times a routine will step without yielding (to prevent lockup from infinite loops)

		private ulong id = noId;
		private IEnumerator enumerator = null;
		private Routine parent = null;
		private Object context = null;
		private ExceptionHandlerDelegate exceptionHandler = null;
		private List<Routine> children = new List<Routine>();
		private List<object> yieldedArray = new List<object>();
		private bool arrayWasYielded = false;
		private bool finishOnAny = false;
		private bool isStepping = false;
		private object returnValue = null;
		private RoutineException error = null;
        private System.Action onStop;

		private static Stack<Routine> pool = new Stack<Routine>();

		private static IResumable resumeImmediately = new ImmediateResumable();
		private static ulong nextId = 0;
		private static Stack<Resumer> resumerPool = new Stack<Resumer>();
		private static Stack<WrappedArray> wrappedArrayPool = new Stack<WrappedArray>();
		private static Stack<Routine> steppingStack = new Stack<Routine>();
		private static object currentReturnValue = null;
		private static List<object> currentAllReturnValues = new List<object>();

		public bool IsDone { get { return id == noId; } }
		public System.Exception Error { get { return error; } }
        
		public void Start(IEnumerator enumerator, Object context = null, ExceptionHandlerDelegate exceptionHandler = null, System.Action onStop = null)
		{
			Stop();
			Setup(enumerator, null, context, exceptionHandler, onStop);
			Step();
		}

		public void Start(IEnumerable enumerable, Object context = null, ExceptionHandlerDelegate exceptionHandler = null, System.Action onStop = null)
		{
			Stop();
			Setup(enumerable.GetEnumerator(), null, context, exceptionHandler, onStop);
			Step();
		}

		public void Stop()
		{
			ClearChildren();
			yieldedArray.Clear();
			arrayWasYielded = false;
			finishOnAny = false;
			isStepping = false;
			id = noId;
            
            var oldOnStop = onStop;
            onStop = null;

            if (oldOnStop != null)
            {
                oldOnStop();
            }
		}

		public void Reset()
		{
			Stop();
			enumerator = null;
			parent = null;
			returnValue = null;
			error = null;
			context = null;
			exceptionHandler = null;
		}

		public Handle GetHandle()
		{
			return new Handle(this, id);
		}

		//Get new routine from pool
		public static Routine Create()
		{
			return (pool.Count > 0) ? pool.Pop() : new Routine();
		}

		//Release routine back to pool
		public static void Release(ref Routine routine)
		{
			routine.Reset();
			pool.Push(routine);
			routine = null;
		}

		//Retrieve the result of the last finished routine
		public static T GetResult<T>()
		{
			return (T)currentReturnValue;
		}

		public static object GetResult()
		{
			return currentReturnValue;
		}

		public static List<object> GetAllResults()
		{
			return currentAllReturnValues;
		}

		//Set the result of the currently stepping routine
		public static void SetResult(object value)
		{
			Assert.IsTrue(steppingStack.Count > 0);
			steppingStack.Peek().returnValue = value;
		}

		//Wrap multiple enumerators to be yielded.
		//Routine won't resume until all have finished.
		//Result will be a List continaing results from all routines. DO NOT alter that list.
		public static IWrappedArray All(params IEnumerator[] yieldables)
		{
			var allArray = (wrappedArrayPool.Count > 0) ? wrappedArrayPool.Pop() : new WrappedArray();
			allArray.yieldables = yieldables;
			allArray.isAny = false;
			return allArray;
		}

		public static IWrappedArray All(IEnumerable yieldables)
		{
			var allArray = (wrappedArrayPool.Count > 0) ? wrappedArrayPool.Pop() : new WrappedArray();
			allArray.yieldables = yieldables;
			allArray.isAny = false;
			return allArray;
		}

		//Wrap multiple enumerators to be yielded.
		//Routine will resume when first one finishes.
		//Result will be value from that first finished routine.
		public static IWrappedArray Any(params IEnumerator[] yieldables)
		{
			var anyArray = (wrappedArrayPool.Count > 0) ? wrappedArrayPool.Pop() : new WrappedArray();
			anyArray.yieldables = yieldables;
			anyArray.isAny = true;
			return anyArray;
		}

		public static IWrappedArray Any(IEnumerable yieldables)
		{
			var anyArray = (wrappedArrayPool.Count > 0) ? wrappedArrayPool.Pop() : new WrappedArray();
			anyArray.yieldables = yieldables;
			anyArray.isAny = true;
			return anyArray;
		}

		//Dummy resumer that resumes immediately upon yielding
		public static IEnumerator ResumeImmediately()
		{
			return resumeImmediately;
		}

		private Routine() {} //Use Routine.Create()

		private void Setup(IEnumerator enumerator, Routine parent, Object context, ExceptionHandlerDelegate exceptionHandler, System.Action onStop = null)
		{
			id = nextId++;
			Assert.IsTrue(nextId != ulong.MaxValue);
			returnValue = null;
			if (parent != null)
			{
				this.parent = parent;
			}
			this.enumerator = enumerator;
			this.context = context;
			this.exceptionHandler = exceptionHandler ?? Debug.LogException;
            this.onStop = onStop;
		}

		private void ClearChildren()
		{
			foreach (var child in children)
			{
				child.Reset();
				pool.Push(child);
			}
			children.Clear();
		}

		private void Finish(RoutineException error)
		{
			this.error = error;
			Stop();
			if (parent != null && !parent.isStepping)
			{
				parent.Step();
			}
		}

		private void Step()
		{
			Assert.IsTrue(!isStepping);
			isStepping = true;

			//Used to detect that routine was killed during step
			var steppingId = id;

			//Running IResumable
			if (enumerator is IResumable)
			{
				var resumer = (resumerPool.Count > 0) ? resumerPool.Pop() : new Resumer();
				resumer.called = false;
				resumer.routine = this;
				resumer.steppingId = steppingId;

				var resumable = enumerator as IResumable;
				resumable.Run(resumer);

				//In case of suicide or immediate finish
				if (steppingId != id)
				{
					return;
				}
			}

			//Running enumerator
			else
			{
				int itCount;
				for (itCount = 0; itCount < Routine.maxIterations; ++itCount)
				{
					currentReturnValue = null;
					currentAllReturnValues.Clear();

					//Bail out if any children are yielding:

					//Any array: stop if all children are yielding
					if (finishOnAny)
					{
						var childIsDone = true;
						for (int i = 0, l = children.Count; i < l; ++i)
						{
							var child = children[i];

							if (child.error != null)
							{
								Finish(child.error);
								return;
							}

							childIsDone = child.IsDone;
							if (childIsDone)
							{
								currentReturnValue = child.returnValue;
								break;
							}
						}
						if (!childIsDone)
						{
							break;
						}
					}

					//Single or all-array: stop if any children are yielding
					else
					{
						var childIsYielding = false;
						for (int i = 0, l = children.Count; i < l; ++i)
						{
							var child = children[i];

							if (child.error != null)
							{
								Finish(child.error);
								return;
							}

							childIsYielding = !child.IsDone;
							if (childIsYielding)
							{
								break;
							}
						}
						if (childIsYielding)
						{
							break;
						}

						//Collect return values from children
						if (arrayWasYielded)
						{
							for (int i = 0, l = children.Count; i < l; ++i)
							{
								currentAllReturnValues.Add(children[i].returnValue);
							}
							currentReturnValue = currentAllReturnValues;
						}
						else if (children.Count > 0)
						{
							Assert.IsTrue(children.Count == 1);
							currentReturnValue = children[0].returnValue;
						}
					}

					//Stop and clear children
					ClearChildren();

					//Step this routine
					steppingStack.Push(this);
					bool done;
					RoutineException stepError = null;
					try
					{
						done = !enumerator.MoveNext();
					}
					catch (System.Exception e)
					{
						stepError = CreateException(e.Message, e);
						exceptionHandler(stepError, context);
						done = true;
					}
					steppingStack.Pop();

					//Check for suicide
					if (steppingId != id)
					{
						return;
					}

					//Prevent GetResult() from giving back something when it shouldn't
					currentReturnValue = null;
					currentAllReturnValues.Clear();

					//Routine finished?
					if (done)
					{
						Finish(stepError);
						return;
					}

					arrayWasYielded = false;
					finishOnAny = false;

					//Check for yielded array
					var result = enumerator.Current;
					if (result is WrappedArray)
					{
						var wrappedArray = result as WrappedArray;
						var arr = wrappedArray.yieldables;
						finishOnAny = wrappedArray.isAny;
						wrappedArrayPool.Push(wrappedArray);

						if (arr == null)
						{
							Finish(CreateException("yieldables not set in WrappedArray"));
							return;
						}

						arrayWasYielded = true;

						//Copy array contents in case one of contained routines messes with it when stepped
						Assert.IsTrue(yieldedArray.Count == 0);
						foreach (var element in arr)
						{
							yieldedArray.Add(element);
						}

						for (int i = 0, l = yieldedArray.Count; i < l; ++i)
						{
							var yieldedValue = yieldedArray[i];
							if (yieldedValue is IEnumerable)
							{
								yieldedValue = (yieldedValue as IEnumerable).GetEnumerator();
							}
							else if (!(yieldedValue is IEnumerator))
							{
								Finish(CreateException(string.Format("yielded value [{0}] is not an IEnumerator: {1}", i, yieldedValue)));
								return;
							}

							var child = CreateChild(yieldedValue as IEnumerator);

							//Check for parenticide
							if (steppingId != id)
							{
								return;
							}

							//Exit if any child completes in any-array mode
							if (finishOnAny && child.IsDone)
							{
								break;
							}
						}

						yieldedArray.Clear();
					}

					//Single runable yielded: create child routine
					else if (result is IEnumerable) //Check for IEnumerable before IEnumerator, as the object could be both (Linq) and we want to treat it as the former if so
					{
						CreateChild((result as IEnumerable).GetEnumerator());

						//Check for parenticide
						if (steppingId != id)
						{
							return;
						}
					}
					else if (result is IEnumerator)
					{
						CreateChild(result as IEnumerator);

						//Check for parenticide
						if (steppingId != id)
						{
							return;
						}
					}

					//Something not-runable was returned
					else
					{
						throw CreateException(string.Format("yielded value is not an IEnumerator or IEnumerable: {0}", result));
					}
				}
				if (itCount == maxIterations)
				{
					throw CreateException("Infinite loop in routine!");
				}
			}

			isStepping = false;
		}

		private Routine CreateChild(IEnumerator enumerator)
		{
			var child = Create();
			child.Setup(enumerator, this, context, exceptionHandler);
			children.Add(child);
			child.Step();
			return child;
		}

		private RoutineException CreateException(string message, System.Exception inner = null)
		{
			var stack = new List<string>();
			stack.Add(message);
			stack.Add("Routine stack:");
			var routine = this;
			do
			{
				stack.Add(string.Format("{0}] {1}", stack.Count - 2, routine.enumerator.ToString()));
				routine = routine.parent;
			}
			while (routine != null);
			message = string.Join("\n", stack.ToArray());
			return (inner != null) ? new RoutineException(message, inner) : new RoutineException(message);
		}
	}
}
