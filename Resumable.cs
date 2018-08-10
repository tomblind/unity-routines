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
using UnityEngine.Assertions;

namespace Routines
{
	//Passed to IResumables to allow control of the routine
	public interface IResumer
	{
		//Check if the routine is still alive. If not, Release() should be called.
		bool IsAlive { get; }

		//Resume the routine and optionally pass a result value back to it.
		void Resume(object result = null);

		//Stop routine and propagate error
		void Throw(System.Exception error);

		//Call to release the IResumer object. Only needs to be used if IsAlive is false.
		void Release();
	}

	//Objects that can be yielded instead of IEnumerators:
	//When yielded, Run() will be called with an object that can
	//be used to resume the coroutine and set a result value.
	public interface IResumable : IEnumerator
	{
		void Run(IResumer resumer);
	}

	//Resumables are 'fake' IEnumerators (they won't work with foreach), simply so they can
	//be passed around easily. If c# had sum-types/unions, this wouldn't be necessary.
	public abstract class Resumable : IResumable
	{
		public abstract void Run(IResumer resumer);

		public virtual object Current { get { throw new System.NotSupportedException(); } }
		public virtual bool MoveNext() { throw new System.NotSupportedException(); }
		public virtual void Reset() { throw new System.NotSupportedException(); }
	}

	//A simple, general purpose resumer
	public class SimpleResumable : Resumable
	{
		public IResumer resumer = null;

		public override void Run(IResumer resumer)
		{
			Assert.IsNull(this.resumer);
			this.resumer = resumer;
		}

		public void Resume(object result = null)
		{
			Assert.IsNotNull(resumer);
			resumer.Resume(result);
			resumer = null;
		}

		public void Throw(System.Exception error)
		{
			Assert.IsNotNull(resumer);
			resumer.Throw(error);
			resumer = null;
		}

		public override void Reset()
		{
			resumer = null;
		}
	}
}
