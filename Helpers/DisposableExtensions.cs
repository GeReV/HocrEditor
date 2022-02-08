using System;
using System.Collections.Generic;

namespace HocrEditor.Helpers;

public static class DisposableExtensions
{
    public static void Dispose(this IEnumerable<IDisposable> disposables)
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    }
}
