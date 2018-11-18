# Routines for Unity

_If you're using Unity 2018.3 or newer, check out [Unity AsyncRoutines](https://github.com/tomblind/unity-async-routines) which accomplishes the same thing, but uses C# 7's async/await for cleaner and more type-safe syntax._

## What Is It?
Routines is a replacement for Unity's coroutines that provides hierarchical support. This means that a running routine can yield on one or more child routines and resume when they complete. Routines also utilize pooling under the hood to reduce garbage generation as much as possible so that they can used extensively without dropping frames.

## Basic Usage
Normally, you want to use a RoutineManager component to manage routines on a GameObject. This will handle all of the details of a routine's lifetime, including stopping it if the object is destroyed.

### Simple Example
```cs
public class MyBehavior : MonoBehaviour
{
    public RoutineManager routineMgr;

    public void Start()
    {
        routineMgr = gameObject.AddComponent<RoutineManager>();
        routineMgr.RunRoutine(MainRoutine());
    }

    public IEnumerator MainRoutine()
    {
        for (var i = 0; i < 5; ++i)
        {
            yield return ChildRoutine(i);
            var result = Routine.GetResult<int>();
            Debug.Log(result);
        }
    }

    public IEnumerator ChildRoutine(int i)
    {
        yield return routineMgr.WaitForSeconds(1);
        Routine.SetResult(i * 2);
    }
}
```

Here we add a RoutineManager and run a simple routine that loops from 0 to 4. It then calls a child which waits for a second and returns the number multiplied by 2, which is received by the parent and sent to the console.

*Protip: You can also directly derive your behavior from RoutineManager to get routine support built in to your component.*

### Mutliple Children
Here you can see multiple routines being yielded at once.

```cs
public IEnumerator DoFirstThing()
{
    yield return routineMgr.WaitForSeconds(1);
    Routine.SetResult(1);
}

public IEnumerator DoSecondThing()
{
    yield return routineMgr.WaitForSeconds(2);
    Routine.SetResult(2);
}

public IEnumerator DoAllTheThingsInOrder()
{
    yield return new IEnumerator[] {DoFirstThing(), DoSecondThing()};
    Debug.Log(Routine.GetResult()); // 2
}

public IEnumerator DoAllTheThingsAtOnce()
{
    yield return Routine.All(DoFirstThing(), DoSecondThing());
    Debug.Log(Routine.GetAllResults()); // List<object> {1, 2}
}

public IEnumerator DoAnyOfTheThings()
{
    yield return Routine.Any(DoFirstThing(), DoSecondThing());
    Debug.Log(Routine.GetResult()); // 1
}
```
DoAllTheThingsInOrder() yields an array (it could be any IEnumerable) which causes the system to execute them in sequence - waiting for each child to finish before starting the next. The result will be whatever the last child set as a result.

DoAllTheThingsAtOnce() uses Routine.All() to specify a set of children which should be executed in parallel. The parent routine will resume once all children have finished and the result will be a List<object> containing the results of each child;

DoAnyOfTheThings() uses Routine.Any(). This is similar to Routine.All() except that it will resume the parent as soon as any of the children finish (stopping the rest). Its result will be from whichever child finished. Determining which one that was is an exercise left to the user.

### Helpers
RoutineManager also provides a number of useful WaitFor...() methods that can be yielded. These are replacements for the built-in Unity YieldInstructions like WaitForSeconds.
- WaitForNextFrame()
- WaitForSeconds()
- WaitUntil()
- WaitForAsyncOperation()
- WaitForCustomYieldInstruction() //Can be used to yield on Unity CustomYieldInstructions, including WWW

## Advanced Usage
### Stopping Routines
RunRoutine() returns a handle to the routine which has a Stop() method. This can be used to stop a routine before it has completed. RoutineManager also has a StopAllRoutines() which stops all routines currently managed by the component.

### Managing Your Own Routines
In some circumstances, it might be ideal to manage the lifecycle of a routine youself.
```cs
public class MyBehavior : MonoBehaviour
{
    public Routine r = null;

    public void Start()
    {
        r = Routine.Create();
        r.Start(MyRoutine());
    }

    public void Update()
    {
        if (r != null && r.IsDone)
        {
            Routine.Release(ref r); //Sets r to null
        }
    }

    public void ForceStop()
    {
        if (r != null)
        {
            r.Stop();
            Routine.Release(ref r);
        }
    }

    public IEnumerator MyRoutine()
    {
        ...
    }
}
```
Routines are pooled and cannot be constructed directly, so use Create() and be sure to call Release() when done with them.

### Error Handling
By default, routines catch exceptions thrown and report them using Debug.LogException(). The routine will be stopped, but not interfere with execution of the rest of the game. You can supply a custom exception handling callback to Start() if you desire other behavior.

### Custom Resumables
In addition to IEnumerator methods, objects implementing IResumable can be yielded as well. These objects should have a Run() method which is called as soon as it is yielded and receives an IResumer. This can be used to resume the routine that yielded the object. This is useful for when a routine needs to wait for a callback (such as a Unity animation event).
```cs
public class MyBehavior : MonoBehaviour
{
    public Animator animator;
    public SimpleResumable resumable = new SimpleResumable();

    public IEnumerator TriggerAnimationAndWait(string trigger)
    {
        animator.SetTrigger(trigger);
        yield return resumable;
        Debug.Log("Animation finished!");
    }

    public void OnAnimationEvent()
    {
        resumable.Resume();
    }
}
```
This example uses SimpleResumable, which catches the IResumer and calls it with Resume(). TriggerAnimationAndWait() sends a trigger to an animator and waits for OnAnimationEvent() to be called. In Unity, an event is set to call that method when the triggered animation finishes.

## Gotchas
- You cannot yield null to wait for the next frame. Use WaitForNextFrame() on RoutineManager.
- Routines have a hard limit on the number of times they can be stepped without yielding something that will wait. This is to prevent Unity from locking up if an inifinite loop is encountered. If you need to iterate more than that limit, you'll have to change maxIterations in Routine.cs and may the powers that be have mercy upon your soul.
- You should always call GetResult() immediately after the yield statement that ran the routine you want the result from. Internally it's stored in a static variable, so it could be replaced if some code runs a new routine before you call it.
- It is safe to yield on methods from other objects than the one with the RoutineManager, but you should make sure those objects aren't destroyed before the routine ends!
- Routine.All() and Routine.Any() cannot be nested. If you need to do this, wrap them in their own IEnumerator functions:
```cs
public IEnumerator DoTheThings()
{
    yield return Routine.All(...);
}

public IEnumerator DoTheOtherThings()
{
    yield return Routine.All(...);
}

public IEnumerator DoAllTheThings()
{
    yield return Routine.All(DoTheThings(), DoTheOtherThings());
}
```
