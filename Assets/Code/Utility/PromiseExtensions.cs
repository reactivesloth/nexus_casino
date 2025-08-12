using System.Threading.Tasks;
using RSG;

public static class PromiseExtensions
{
    public static Task<T> ToTask<T>(this IPromise<T> promise)
    {
        var tcs = new TaskCompletionSource<T>();
        promise.Then(tcs.SetResult).Catch(e => tcs.SetException(e));
        return tcs.Task;
    }

    public static Task ToTask(this IPromise promise)
    {
        var tcs = new TaskCompletionSource<bool>();
        promise.Then(() => tcs.SetResult(true)).Catch(e => tcs.SetException(e));
        return tcs.Task;
    }
}